namespace Authly.Core.Entities;

/// <summary>
/// A client secret for a confidential <see cref="Application"/>. The raw secret
/// (<c>secret_[48]</c>) is shown to the tenant exactly once at creation/rotation; only its
/// Argon2id hash is stored. Multiple may coexist to allow zero-downtime rotation. Maps to
/// table "application_secrets".
/// </summary>
public class ApplicationSecret
{
    public Guid Id { get; set; }

    public Guid ApplicationId { get; set; }

    /// <summary>Argon2id hash of the raw secret. Never store the raw value.</summary>
    public string SecretHash { get; set; } = default!;

    public string? Label { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
    public bool Revoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Application Application { get; set; } = default!;
}
