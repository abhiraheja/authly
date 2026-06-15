using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// One event queued for delivery to one endpoint (§4.12). The render of the signed body lives in
/// <see cref="Payload"/> (JSON); retries advance <see cref="Attempts"/> and reschedule
/// <see cref="NextRetryAt"/> on the exponential-backoff ladder. Maps to table "webhook_deliveries".
/// </summary>
public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid EndpointId { get; set; }
    public Guid TenantId { get; set; }

    public string Event { get; set; } = default!;

    /// <summary>The exact JSON body that is (or was) POSTed and signed. JSONB.</summary>
    public string Payload { get; set; } = "{}";

    public WebhookDeliveryStatus Status { get; set; } = WebhookDeliveryStatus.Pending;
    public int Attempts { get; set; }

    /// <summary>HTTP status returned by the most recent attempt, if any.</summary>
    public int? ResponseCode { get; set; }

    /// <summary>Short diagnostic from the last failed attempt (never contains the payload/secret).</summary>
    public string? LastError { get; set; }

    /// <summary>When the next retry is due; null once delivered or permanently failed.</summary>
    public DateTimeOffset? NextRetryAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
