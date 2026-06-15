namespace Authly.Core.Events;

/// <summary>
/// Publishes a domain event for webhook fan-out (§4.12). Implemented in the module layer; finds the
/// tenant's subscribed endpoints, records a delivery per endpoint, and enqueues dispatch. Must never
/// throw into the caller — a publish failure cannot break the audited operation that triggered it.
/// </summary>
public interface IEventPublisher
{
    Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default);
}

/// <summary>The immutable record of a domain event, serialized as the webhook body's core.</summary>
/// <param name="Event">Catalogue event name (e.g. <c>user.login</c>).</param>
/// <param name="TenantId">Owning tenant; routing + RLS boundary.</param>
/// <param name="OccurredAt">When the event happened (UTC).</param>
/// <param name="ActorId">Who caused it, if known.</param>
/// <param name="ActorType">user | super_admin | api_key | system.</param>
/// <param name="ResourceType">Affected resource type, if any.</param>
/// <param name="ResourceId">Affected resource id, if any.</param>
/// <param name="Result">success | failure | blocked.</param>
/// <param name="Data">Non-sensitive metadata (never secrets/credentials).</param>
public sealed record EventEnvelope(
    string Event,
    Guid TenantId,
    DateTimeOffset OccurredAt,
    Guid? ActorId,
    string? ActorType,
    string? ResourceType,
    Guid? ResourceId,
    string Result,
    object? Data);

/// <summary>
/// Enqueues webhook delivery dispatch out of band. Implemented over Hangfire in the composition
/// root; module services depend only on this. <see cref="Schedule"/> backs the retry ladder. The
/// tenant id rides along so the dispatch job can set the RLS scope before loading the delivery
/// (dispatch runs outside an HTTP request).
/// </summary>
public interface IWebhookQueue
{
    void Enqueue(Guid tenantId, Guid deliveryId);
    void Schedule(Guid tenantId, Guid deliveryId, TimeSpan delay);
}

/// <summary>
/// Transport that POSTs a signed webhook body to an endpoint. Implemented in Infrastructure
/// (HttpClient). Never throws — network/HTTP failures come back as <see cref="WebhookSendResult"/>.
/// </summary>
public interface IWebhookSender
{
    Task<WebhookSendResult> SendAsync(WebhookSendRequest request, CancellationToken ct = default);
}

/// <param name="Url">Target endpoint.</param>
/// <param name="Secret">Decrypted HMAC secret.</param>
/// <param name="Event">Event name (sent as a header).</param>
/// <param name="DeliveryId">Unique delivery id (sent as a header for idempotency).</param>
/// <param name="Body">The exact JSON body to POST and sign.</param>
public sealed record WebhookSendRequest(string Url, string Secret, string Event, Guid DeliveryId, string Body);

/// <param name="Success">True on a 2xx response.</param>
/// <param name="StatusCode">HTTP status returned, if a response was received.</param>
/// <param name="Error">Short diagnostic on failure (no body/secret).</param>
public sealed record WebhookSendResult(bool Success, int? StatusCode, string? Error)
{
    public static WebhookSendResult Ok(int status) => new(true, status, null);
    public static WebhookSendResult Fail(int? status, string error) => new(false, status, error);
}
