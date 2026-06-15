namespace Authly.Core.Entities;

/// <summary>
/// Links a user to an external identity provider account (Google, GitHub, …). Provider tokens
/// are AES-256-GCM encrypted at rest. Scoped to a tenant — the same provider account in two
/// tenants is two separate identities (tenant isolation over the spec's global uniqueness).
/// Maps to table "social_identities".
/// </summary>
public class SocialIdentity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>google | facebook | github | microsoft | … (or a custom provider key).</summary>
    public string Provider { get; set; } = default!;

    /// <summary>The stable subject id at the provider (e.g. Google "sub").</summary>
    public string ProviderId { get; set; } = default!;

    public string? ProviderEmail { get; set; }

    /// <summary>AES-256 encrypted access token.</summary>
    public string? AccessToken { get; set; }

    /// <summary>AES-256 encrypted refresh token.</summary>
    public string? RefreshToken { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>The raw provider profile JSON (no secrets).</summary>
    public string RawProfile { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
}
