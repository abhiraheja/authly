using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Events;
using Authly.Core.Interfaces;
using Authly.Infrastructure.Security;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Webhooks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authly.Tests.Webhooks;

public class WebhookServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // --- EventPublisher routing --------------------------------------------

    [Fact]
    public async Task Publisher_creates_a_delivery_per_matching_endpoint_and_enqueues()
    {
        var h = new Harness();
        h.Endpoints.Items.Add(Endpoint("https://a/hook", new[] { "user.login" }));      // matches
        h.Endpoints.Items.Add(Endpoint("https://b/hook", new[] { "*" }));                 // wildcard matches
        h.Endpoints.Items.Add(Endpoint("https://c/hook", new[] { "user.created" }));      // no match
        h.Endpoints.Items.Add(Endpoint("https://d/hook", new[] { "user.login" }, active: false)); // inactive

        await h.Publisher.PublishAsync(Envelope("user.login"));

        Assert.Equal(2, h.Deliveries.Items.Count);
        Assert.Equal(2, h.Queue.Enqueued.Count);
        Assert.All(h.Deliveries.Items, d => Assert.Equal(WebhookDeliveryStatus.Pending, d.Status));
        // The signed body carries the delivery id and event name.
        Assert.All(h.Deliveries.Items, d => Assert.Contains(d.Id.ToString(), d.Payload));
        Assert.All(h.Deliveries.Items, d => Assert.Contains("user.login", d.Payload));
    }

    [Fact]
    public async Task Publisher_is_silent_when_no_endpoint_subscribes()
    {
        var h = new Harness();
        h.Endpoints.Items.Add(Endpoint("https://a/hook", new[] { "user.created" }));

        await h.Publisher.PublishAsync(Envelope("user.login"));

        Assert.Empty(h.Deliveries.Items);
        Assert.Empty(h.Queue.Enqueued);
    }

    // --- Dispatch + retry ladder -------------------------------------------

    [Fact]
    public async Task Dispatch_success_marks_delivery_success()
    {
        var h = new Harness();
        var endpoint = Endpoint("https://a/hook", new[] { "*" });
        endpoint.Secret = h.Encryption.Encrypt("secret");
        h.Endpoints.Items.Add(endpoint);
        var delivery = Delivery(endpoint.Id);
        h.Deliveries.Items.Add(delivery);
        h.Sender.Result = WebhookSendResult.Ok(200);

        await h.Service.DispatchAsync(delivery.Id);

        Assert.Equal(WebhookDeliveryStatus.Success, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(200, delivery.ResponseCode);
        Assert.Null(delivery.NextRetryAt);
        Assert.Empty(h.Queue.Scheduled);
    }

    [Fact]
    public async Task Dispatch_failure_schedules_next_retry_on_the_ladder()
    {
        var h = new Harness();
        var endpoint = Endpoint("https://a/hook", new[] { "*" });
        endpoint.Secret = h.Encryption.Encrypt("secret");
        h.Endpoints.Items.Add(endpoint);
        var delivery = Delivery(endpoint.Id);
        h.Deliveries.Items.Add(delivery);
        h.Sender.Result = WebhookSendResult.Fail(500, "HTTP 500");

        await h.Service.DispatchAsync(delivery.Id);

        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.NotNull(delivery.NextRetryAt);
        var scheduled = Assert.Single(h.Queue.Scheduled);
        Assert.Equal(TimeSpan.FromMinutes(1), scheduled.Delay); // first retry delay
    }

    [Fact]
    public async Task Dispatch_marks_failed_once_the_ladder_is_exhausted()
    {
        var h = new Harness();
        var endpoint = Endpoint("https://a/hook", new[] { "*" });
        endpoint.Secret = h.Encryption.Encrypt("secret");
        h.Endpoints.Items.Add(endpoint);
        var delivery = Delivery(endpoint.Id);
        delivery.Attempts = WebhookRetrySchedule.MaxAttempts - 1; // one attempt left
        h.Deliveries.Items.Add(delivery);
        h.Sender.Result = WebhookSendResult.Fail(503, "HTTP 503");

        await h.Service.DispatchAsync(delivery.Id);

        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Null(delivery.NextRetryAt);
        Assert.Empty(h.Queue.Scheduled);
    }

    [Fact]
    public async Task Save_endpoint_encrypts_the_secret_and_generates_one_when_blank()
    {
        var h = new Harness();
        await h.Service.SaveEndpointAsync(Tenant, new WebhookEndpointInput
        {
            Url = "https://a/hook", Events = new[] { "user.login" }, Secret = null, IsActive = true
        }, AuditContext.System);

        var saved = Assert.Single(h.Endpoints.Items);
        Assert.NotEqual("generated-secret", saved.Secret);            // stored encrypted, not raw
        Assert.Equal("generated-secret", h.Encryption.Decrypt(saved.Secret));
    }

    // --- harness ------------------------------------------------------------

    private static WebhookEndpoint Endpoint(string url, string[] events, bool active = true) => new()
    {
        Id = Guid.NewGuid(), TenantId = Tenant, Url = url, Events = events, Secret = "enc", IsActive = active,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static WebhookDelivery Delivery(Guid endpointId) => new()
    {
        Id = Guid.NewGuid(), EndpointId = endpointId, TenantId = Tenant, Event = "user.login",
        Payload = """{"event":"user.login"}""", Status = WebhookDeliveryStatus.Pending, CreatedAt = DateTimeOffset.UtcNow
    };

    private static EventEnvelope Envelope(string evt) => new(
        evt, Tenant, DateTimeOffset.UtcNow, Guid.NewGuid(), "user", "user", Guid.NewGuid(), "success", new { foo = "bar" });

    private sealed class Harness
    {
        public readonly FakeEndpointRepo Endpoints = new();
        public readonly FakeDeliveryRepo Deliveries = new();
        public readonly FakeQueue Queue = new();
        public readonly FakeSender Sender = new();
        public readonly AesEncryptionService Encryption =
            new(Options.Create(new EncryptionOptions { Key = "3J8mZ1qg9X0vQpYb2sR7tU4wK6nL5cD8eF1aH0iJ2kM=" }));
        public readonly EventPublisher Publisher;
        public readonly WebhookService Service;

        public Harness()
        {
            Publisher = new EventPublisher(Endpoints, Deliveries, Queue, NullLogger<EventPublisher>.Instance);
            Service = new WebhookService(Endpoints, Deliveries, Sender, Queue, Encryption,
                new FakeCredentials(), new NullAudit(), NullLogger<WebhookService>.Instance);
        }
    }

    private sealed class FakeEndpointRepo : IWebhookEndpointRepository
    {
        public readonly List<WebhookEndpoint> Items = new();
        public Task<IReadOnlyList<WebhookEndpoint>> ListByTenantAsync(Guid t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WebhookEndpoint>>(Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<WebhookEndpoint>> ListMatchingAsync(Guid t, string e, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WebhookEndpoint>>(Items
                .Where(x => x.TenantId == t && x.IsActive && (x.Events.Contains(e) || x.Events.Contains("*"))).ToList());
        public Task<WebhookEndpoint?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task AddAsync(WebhookEndpoint e, CancellationToken ct = default) { if (e.Id == Guid.Empty) e.Id = Guid.NewGuid(); Items.Add(e); return Task.CompletedTask; }
        public Task UpdateAsync(WebhookEndpoint e, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(WebhookEndpoint e, CancellationToken ct = default) { Items.Remove(e); return Task.CompletedTask; }
    }

    private sealed class FakeDeliveryRepo : IWebhookDeliveryRepository
    {
        public readonly List<WebhookDelivery> Items = new();
        public Task AddAsync(WebhookDelivery d, CancellationToken ct = default) { if (d.Id == Guid.Empty) d.Id = Guid.NewGuid(); Items.Add(d); return Task.CompletedTask; }
        public Task UpdateAsync(WebhookDelivery d, CancellationToken ct = default) => Task.CompletedTask;
        public Task<WebhookDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
        public Task<IReadOnlyList<WebhookDelivery>> ListRecentByTenantAsync(Guid t, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WebhookDelivery>>(Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<WebhookDelivery>> ListByEndpointAsync(Guid t, Guid ep, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WebhookDelivery>>(Items.Where(x => x.TenantId == t && x.EndpointId == ep).ToList());
    }

    private sealed class FakeQueue : IWebhookQueue
    {
        public readonly List<Guid> Enqueued = new();
        public readonly List<(Guid Id, TimeSpan Delay)> Scheduled = new();
        public void Enqueue(Guid tenantId, Guid deliveryId) => Enqueued.Add(deliveryId);
        public void Schedule(Guid tenantId, Guid deliveryId, TimeSpan delay) => Scheduled.Add((deliveryId, delay));
    }

    private sealed class FakeSender : IWebhookSender
    {
        public WebhookSendResult Result = WebhookSendResult.Ok(200);
        public Task<WebhookSendResult> SendAsync(WebhookSendRequest request, CancellationToken ct = default)
            => Task.FromResult(Result);
    }

    private sealed class FakeCredentials : ICredentialGenerator
    {
        public string GenerateClientId() => "client_x";
        public string GenerateClientSecret() => "generated-secret";
        public string GenerateApiKey() => "authly_sk_x";
        public string GenerateNumericOtp(int digits = 6) => "123456";
        public string GenerateBackupCode() => "abcde-fghij";
    }

    private sealed class NullAudit : IAuditLogger
    {
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
            Guid? resourceId = null, string result = "success", object? metadata = null, bool publishEvent = true, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
