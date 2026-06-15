namespace Authly.Core.Entities;

/// <summary>
/// A tenant's outbound webhook target (§4.12). Events whose name is listed in <see cref="Events"/>
/// (or the wildcard <c>*</c>) are delivered here, HMAC-SHA256 signed with <see cref="Secret"/>.
/// The secret is AES-encrypted at rest and decrypted only when signing a delivery.
/// Maps to table "webhook_endpoints".
/// </summary>
public class WebhookEndpoint
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>HTTPS endpoint that receives the signed POST.</summary>
    public string Url { get; set; } = default!;

    /// <summary>Event names this endpoint subscribes to; <c>*</c> matches every event.</summary>
    public string[] Events { get; set; } = Array.Empty<string>();

    /// <summary>HMAC signing secret, AES-encrypted at rest.</summary>
    public string Secret { get; set; } = default!;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
