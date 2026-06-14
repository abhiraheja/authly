---
name: Azure Service Bus — producers, consumers, blob offload
description: Topic-based Service Bus producer/consumer base classes with managed-identity auth, >256KB blob offload, duplicate detection, dead-lettering, SSRF-safe blob fetch, and reflection-based auto-registration of producers (scoped) and consumers (hosted services).
type: skill-section
---

# Azure Service Bus — producers, consumers, blob offload

## When to use

Every Saarvix service that publishes or subscribes to domain events uses Azure Service Bus with **topics** (not queues). Each service owns a dedicated `{ServiceName}.AzureServiceBus` project that compiles into a separate assembly — not buried in Infrastructure — so its types can be reflection-scanned for auto-registration.

## Architectural decisions

- **Topics, not queues.** Multiple subscribers per event is the norm (audit + analytics + email + webhook). Queues collapse this into a single consumer.
- **Duplicate detection enabled at topic creation** (`RequiresDuplicateDetection = true`, 10-minute window). Protects against double-publish from MediatR notification retries.
- **Blob offload for >256KB payloads.** Service Bus Standard caps at 256KB. Rather than refuse large messages, serialize to blob, send a pointer. Consumers fetch and deserialize transparently.
- **Managed identity (`DefaultAzureCredential`) everywhere** — no connection strings, no SAS tokens. Same credential for Service Bus and Blob Storage means one identity to grant permissions to.
- **`ApplicationProperties` carry metadata, body carries payload.** Filtering by `source`, `event-type`, `application-id` happens at subscription filter level without parsing the body.
- **Producers are scoped, consumers are `IHostedService` singletons.** Producers participate in the request pipeline; consumers run for the process lifetime.
- **Double-checked async initialization** for topic/blob existence — no blocking I/O in constructors, no repeated admin calls on every send.
- **Consumer reuses its `DefaultAzureCredential`** across Service Bus + Blob clients so token cache is shared.
- **SSRF guard on blob pointer.** The consumer must verify the incoming `blob-url` is within the trusted container before fetching. Without this, a crafted message tricks the service's managed identity into fetching arbitrary Azure URLs.

## Project layout

```
{ServiceName}.AzureServiceBus/
├── AzureServiceBusInstaller.cs          // reflection-based DI
├── Base/
│   ├── ServiceBusProducerService.cs     // abstract base for all producers
│   └── ServiceBusConsumerService.cs     // abstract BackgroundService for consumers
├── Producers/
│   └── EmailSenderProducer.cs           // one file per topic
└── Consumers/
    └── {...}Consumer.cs
```

Producer **interfaces** live in Application (`Application/Interfaces/AzureEventService/`), not in AzureServiceBus, so other layers can depend on the abstraction without referencing the service-bus SDK.

## `ServiceBusConfiguration` POCO (`Models/Base/ServiceBusConfiguration.cs`)

```csharp
public class ServiceBusConfiguration
{
    public required string FullyQualifiedNamespace { get; set; }  // "{ns}.servicebus.windows.net"
    public required string BlobServiceUrl          { get; set; }  // "https://{account}.blob.core.windows.net"
    public required string ContainerName           { get; set; }  // blob container for offloaded messages
    public string?         ApplicationName         { get; set; }  // filled in by installer from config
}
```

`appsettings.json`:

```json
{
  "ApplicationName": "{ServiceName}",
  "ServiceBusConfiguration": {
    "FullyQualifiedNamespace": "saarvix-dev.servicebus.windows.net",
    "BlobServiceUrl":          "https://saarvixevents.blob.core.windows.net",
    "ContainerName":           "event-payloads"
  }
}
```

## Producer base: `ServiceBusProducerService<TClass>`

```csharp
public abstract class ServiceBusProducerService<TClass> : IServiceBusProducerService, IAsyncDisposable
    where TClass : class
{
    private const int ServiceBusMaxSizeKB = 256;

    private readonly ServiceBusClient _client;
    private readonly ServiceBusSender _sender;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly BlobContainerClient _blobContainer;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    protected readonly string _topicName;
    protected readonly ILogger<TClass> _logger;
    private readonly string _sourceName;

    protected ServiceBusProducerService(
        string topicName,
        ServiceBusConfiguration busConfig,
        ILogger<TClass> logger,
        string sourceName)
    {
        _topicName   = topicName;
        _logger      = logger;
        _sourceName  = sourceName;

        var credential = new DefaultAzureCredential();
        _client        = new ServiceBusClient(busConfig.FullyQualifiedNamespace, credential);
        _adminClient   = new ServiceBusAdministrationClient(busConfig.FullyQualifiedNamespace, credential);
        _sender        = _client.CreateSender(topicName);  // sync, no I/O — safe in ctor

        var blobServiceClient = new BlobServiceClient(new Uri(busConfig.BlobServiceUrl), credential);
        _blobContainer        = blobServiceClient.GetBlobContainerClient(busConfig.ContainerName);
    }

    // Called once before first send; double-checked locking prevents duplicate init under concurrency
    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized) return;
        await _initLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            await _blobContainer.CreateIfNotExistsAsync(cancellationToken: ct);
            await EnsureTopicExistsAsync(ct);
            _initialized = true;
        }
        finally { _initLock.Release(); }
    }

    private async Task EnsureTopicExistsAsync(CancellationToken ct)
    {
        if (!await _adminClient.TopicExistsAsync(_topicName, ct))
        {
            await _adminClient.CreateTopicAsync(new CreateTopicOptions(_topicName)
            {
                RequiresDuplicateDetection = true,
                DuplicateDetectionHistoryTimeWindow = TimeSpan.FromMinutes(10)
            }, ct);
        }
    }

    public async Task ProduceAsync<TMessage>(
        SendMessageModel<TMessage> request,
        string applicationId,
        string applicationName,
        CancellationToken ct = default)
        where TMessage : class
    {
        var messageId = Guid.NewGuid().ToString();
        await EnsureInitializedAsync(ct);

        var bodyBytes  = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request));
        var sentToBlob = false;
        string? blobUrl = null;

        if (bodyBytes.Length > ServiceBusMaxSizeKB * 1024)
        {
            blobUrl    = await UploadToBlobAsync(messageId, bodyBytes, ct);
            sentToBlob = true;

            var pointerPayload = new
            {
                MessageId         = messageId,
                BlobUrl           = blobUrl,
                OriginalSizeBytes = bodyBytes.Length,
                Type              = typeof(TMessage).Name
            };
            bodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(pointerPayload));
        }

        var message = new ServiceBusMessage(bodyBytes)
        {
            MessageId   = messageId,
            Subject     = request.EventName,
            ContentType = "application/json"
        };
        message.ApplicationProperties["x-correlation-id"]  = messageId;
        message.ApplicationProperties["x-date"]            = request.Date.ToUnixTimeSeconds();
        message.ApplicationProperties["x-changed-by"]      = request.ChangedBy;
        message.ApplicationProperties["source"]            = _sourceName;
        message.ApplicationProperties["event-type"]        = request.EventName;
        message.ApplicationProperties["timestamp"]         = DateTime.UtcNow.ToString("o");
        message.ApplicationProperties["application-id"]    = applicationId;
        message.ApplicationProperties["application-name"]  = applicationName;
        message.ApplicationProperties["payload-offloaded"] = sentToBlob;
        if (sentToBlob)
            message.ApplicationProperties["blob-url"] = blobUrl;

        // Let exceptions propagate — callers need to retry, compensate, or surface.
        await _sender.SendMessageAsync(message, ct);

        _logger.LogInformation("{MessageId}: Event {EventName} published (BlobOffload={Blob})",
            messageId, request.EventName, sentToBlob);
    }

    private async Task<string> UploadToBlobAsync(string messageId, byte[] payload, CancellationToken ct)
    {
        var blobName   = $"{_topicName}/{DateTime.UtcNow:yyyy/MM/dd}/{messageId}.json";
        var blobClient = _blobContainer.GetBlobClient(blobName);
        using var stream = new MemoryStream(payload);
        await blobClient.UploadAsync(stream, overwrite: true, ct);
        return blobClient.Uri.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _client.DisposeAsync();
        _initLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

## Concrete producer pattern (`Producers/EmailSenderProducer.cs`)

```csharp
public class EmailSenderProducer : ServiceBusProducerService<EmailSenderProducer>, IEmailSenderProducer
{
    public const string topicName = "email-trigger";
    private const string SystemUser = "system";
    private const string AppId      = "saar-auth";
    private const string AppName    = "{ServiceName}";

    public EmailSenderProducer(
        ServiceBusConfiguration configuration,
        ILogger<EmailSenderProducer> logger,
        // ... other deps ...
    ) : base(topicName, configuration, logger, configuration.ApplicationName!) { }

    public async Task SendEmailVerificationAsync(string name, string email, string otp, CancellationToken ct = default)
    {
        var model = new SendEmailModel { /* subject, body, recipients, ... */ };
        var msg   = new SendMessageModel<SendEmailModel>(
            DateTimeOffset.UtcNow, SystemUser, model, MessageTypeConstants.EmailMessageEvent);

        await ProduceAsync(msg, AppId, AppName, ct);

        // NEVER log the OTP / secret material
        _logger.LogInformation("[EMAIL] Verification email queued for {Email}", email);
    }
}
```

**Rules for concrete producers:**

1. Topic name is a `public const string` so consumers in other services can reference it.
2. One producer class per topic. Don't create a multi-topic producer.
3. Implement a narrow interface (`IEmailSenderProducer`) declared in Application. The installer auto-discovers implementations via reflection.
4. Pass `configuration.ApplicationName` as the `sourceName` so downstream consumers can identify the publishing service.
5. **Never log secrets or PII** that appear in the payload (OTPs, tokens, card numbers, full emails with PII context).

## Consumer base: `ServiceBusConsumerService<TClass, TMessageHandlerType>`

```csharp
public abstract class ServiceBusConsumerService<TClass, TMessageHandlerType> : BackgroundService
    where TClass : class
    where TMessageHandlerType : ConsumerNotification   // a MediatR INotification base class
{
    private readonly ILogger<TClass> _logger;
    private readonly string _topicName;
    private readonly string _subscriptionName;
    private readonly ServiceBusClient _client;
    private readonly ServiceBusAdministrationClient _adminClient;
    private readonly ServiceBusProcessor _processor;
    private readonly BlobContainerClient _blobContainer;
    private readonly DefaultAzureCredential _credential;
    protected readonly IServiceProvider _serviceProvider;

    protected ServiceBusConsumerService(
        string topicName,
        string subscriptionName,
        ServiceBusConfiguration busConfig,
        ILogger<TClass> logger,
        IServiceProvider serviceProvider)
    {
        _logger           = logger;
        _topicName        = topicName;
        _subscriptionName = subscriptionName;
        _serviceProvider  = serviceProvider;
        _credential       = new DefaultAzureCredential();

        _client        = new ServiceBusClient(busConfig.FullyQualifiedNamespace, _credential);
        _adminClient   = new ServiceBusAdministrationClient(busConfig.FullyQualifiedNamespace, _credential);
        _blobContainer = new BlobServiceClient(new Uri(busConfig.BlobServiceUrl), _credential)
                            .GetBlobContainerClient(busConfig.ContainerName);

        _processor = _client.CreateProcessor(_topicName, _subscriptionName, new ServiceBusProcessorOptions
        {
            AutoCompleteMessages       = false,
            MaxConcurrentCalls         = 10,
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(10)
        });
        _processor.ProcessMessageAsync += ProcessMessageHandler;
        _processor.ProcessErrorAsync   += ProcessErrorHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _blobContainer.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        await EnsureTopicAndSubscriptionExistAsync();

        await _processor.StartProcessingAsync(stoppingToken);
        try { await Task.Delay(Timeout.Infinite, stoppingToken); }
        catch (OperationCanceledException) { }
        finally { await _processor.StopProcessingAsync(CancellationToken.None); }
    }

    // ... EnsureTopicAndSubscriptionExistAsync / process handlers below ...
}
```

**Subscription creation options (non-negotiable):**

```csharp
new CreateSubscriptionOptions(_topicName, _subscriptionName)
{
    MaxDeliveryCount                 = 10,
    DeadLetteringOnMessageExpiration = true
}
```

After 10 failed deliveries or TTL expiry, the message moves to the Dead-Letter Queue for investigation. Never use `MaxDeliveryCount = 1` — a transient Mongo blip would permanently lose the event.

### Processor options

| Option | Value | Why |
|---|---|---|
| `AutoCompleteMessages` | `false` | Only complete after successful handler execution; otherwise lost messages on crash |
| `MaxConcurrentCalls` | `10` | Matches MaxConnectionPoolSize on Mongo — avoids thundering the DB |
| `MaxAutoLockRenewalDuration` | `10 min` | Long-running handlers (e.g., email render + send) don't lose their lock |

### Message handler — SSRF-safe blob fetch + MediatR publish

```csharp
private async Task ProcessMessageHandler(ProcessMessageEventArgs args)
{
    var message   = args.Message;
    var messageId = message.MessageId;

    try
    {
        string json;

        if (message.ApplicationProperties.TryGetValue("payload-offloaded", out var offloadedObj)
            && offloadedObj is bool offloaded && offloaded)
        {
            var blobUrl = message.ApplicationProperties["blob-url"]!.ToString()!;

            // SSRF GUARD — required. Without this, a crafted message makes the service
            // fetch arbitrary Azure URLs using its own managed identity.
            if (!blobUrl.StartsWith(_blobContainer.Uri.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("{MessageId}: Rejected — blob URL outside expected container", messageId);
                await args.DeadLetterMessageAsync(message, "INVALID_BLOB_URL",
                    "Blob URL is outside the expected container");
                return;
            }

            json = await DownloadFromBlobAsync(blobUrl);
        }
        else
        {
            json = Encoding.UTF8.GetString(message.Body);
        }

        var deserialized = Deserialize(json)?.Message;
        if (deserialized is null)
        {
            await args.DeadLetterMessageAsync(message, "DESERIALIZATION_FAILED");
            return;
        }

        // Hydrate transport metadata onto the notification
        if (Guid.TryParse(messageId, out var guid)) deserialized.MessageId = guid;
        deserialized.EventTypeId = GetAppProp(message, "event-type");
        deserialized.Source      = GetAppProp(message, "source");
        deserialized.ChangedBy   = GetAppProp(message, "x-changed-by");

        // ... date/timestamp parsing ...

        using var scope    = _serviceProvider.CreateScope();
        var       mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await mediator.Publish(deserialized, args.CancellationToken);
        await args.CompleteMessageAsync(message);
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "{MessageId}: Error processing message — will retry / DLQ", messageId);
        // DO NOT call CompleteMessageAsync here — lock expires, Service Bus retries, DLQ after MaxDeliveryCount
    }
}
```

**Concrete consumer skeleton:**

```csharp
public class EmailEventConsumer : ServiceBusConsumerService<EmailEventConsumer, EmailConsumerNotification>
{
    public EmailEventConsumer(
        ServiceBusConfiguration busConfig,
        ILogger<EmailEventConsumer> logger,
        IServiceProvider sp)
        : base(EmailSenderProducer.topicName, "email-worker", busConfig, logger, sp) { }
}
```

Then implement a MediatR `INotificationHandler<EmailConsumerNotification>` in Application to do the actual work. The consumer is a pure transport wrapper.

## Auto-registration via reflection: `AzureServiceBusInstaller`

```csharp
public static class AzureServiceBusInstaller
{
    public static IServiceCollection AddAzureServiceBusInstaller(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var busCfg = configuration.GetSection("ServiceBusConfiguration").Get<ServiceBusConfiguration>()!;
        busCfg.ApplicationName = configuration.GetValue<string>("ApplicationName");
        services.AddSingleton(busCfg);

        var allTypes          = Assembly.GetExecutingAssembly().GetTypes();
        var producerInterface = typeof(IServiceBusProducerService);

        // Producers → scoped, registered against their narrow interface (IEmailSenderProducer, ...)
        foreach (var type in allTypes)
        {
            if (!type.IsClass || type.IsAbstract) continue;
            if (!producerInterface.IsAssignableFrom(type)) continue;

            var narrowInterface = type.GetInterfaces()
                .FirstOrDefault(i => i != producerInterface && producerInterface.IsAssignableFrom(i));
            if (narrowInterface is null) continue;

            services.AddScoped(narrowInterface, type);
        }

        // Consumers → IHostedService singletons (BackgroundService)
        var consumers = allTypes.Where(t =>
            t.IsClass && !t.IsAbstract && !t.IsInterface &&
            t.BaseType is { IsGenericType: true } bt &&
            bt.GetGenericTypeDefinition() == typeof(ServiceBusConsumerService<,>));

        foreach (var consumer in consumers)
            services.AddSingleton(typeof(IHostedService), consumer);

        return services;
    }
}
```

**Why reflection scan?** Adding a new producer/consumer must not require editing the installer. Drop a new class into `Producers/` or `Consumers/`, and DI picks it up.

**Why narrow interface lookup?** Registering against `IServiceBusProducerService` directly would mean only one producer can resolve. The narrow interface (`IEmailSenderProducer`) makes the producer addressable by purpose.

## Call-site wiring (`Program.cs`)

```csharp
builder.Services.AddAzureServiceBusInstaller(builder.Configuration);
```

Consumers start automatically because they are registered as `IHostedService` and ASP.NET Core hosts them.

## Payload size & blob offload accounting

- Service Bus Standard: 256KB absolute max.
- Service Bus Premium: 1MB (up to 100MB with large message support).
- **Saarvix default = 256KB threshold** for portability across SKUs. Don't raise even on Premium — stay compatible with Standard for disaster-recovery.
- Blob path convention: `{topic}/{yyyy/MM/dd}/{messageId}.json`. Makes retention policies and forensic lookup trivial.

## Dead-letter handling

A message hitting DLQ means a bug you need to investigate. Do not auto-reprocess DLQ. Standard response:

1. Alert on DLQ depth > 0.
2. Inspect the message (reason code in `DeadLetterReason`).
3. Fix the consumer code or the producer payload.
4. Manually requeue via Service Bus Explorer / Azure CLI.

## Common mistakes

1. **Calling admin APIs on every send.** Use the `EnsureInitializedAsync` / double-checked lock pattern. `CreateTopicAsync` is expensive.
2. **Forgetting the SSRF guard on `blob-url`.** Any inbound message becomes a gateway to exfiltration.
3. **Logging OTPs / tokens / PII from message bodies.** One line in production logs and you have a compliance incident.
4. **Setting `AutoCompleteMessages = true`.** A crash mid-handler silently loses the message.
5. **Registering the consumer as `IHostedService` manually in Program.cs AND via the reflection scan.** Double-registration leads to double processing. Pick one (the reflection scan).
6. **Using `MaxDeliveryCount = 1`.** Transient DB errors permanently lose events.
7. **Creating a new `DefaultAzureCredential` per operation.** Breaks token cache; hammers the IMDS endpoint. Reuse one credential per class.

## Related skills

- `05-mediatr-and-pipeline-behaviors.md` — how `mediator.Publish` dispatches to `INotificationHandler`s.
- `10-azure-keyvault-and-configuration.md` — where the service bus configuration comes from.
- `01-solution-layout-and-reference-chain.md` — why AzureServiceBus is a sibling project to Infrastructure.
