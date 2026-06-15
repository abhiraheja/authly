namespace Authly.Core.Entities;

/// <summary>
/// A tenant's configuration for one social/OAuth2 login provider. The
/// <see cref="ClientSecret"/> is AES-256 encrypted at rest. Known providers (google, github, …)
/// take their endpoints from built-in presets; a "custom" provider supplies its own endpoints
/// for generic OAuth2/OIDC. Unique per (tenant, provider). Maps to table "social_providers".
/// </summary>
public class SocialProvider
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Provider key: google | github | microsoft | facebook | custom | …</summary>
    public string Provider { get; set; } = default!;

    public string ClientId { get; set; } = default!;

    /// <summary>AES-256 encrypted client secret.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Space-delimited OAuth scopes; empty falls back to the preset's defaults.</summary>
    public string? Scopes { get; set; }

    // Generic OAuth2/OIDC endpoints (used when no preset exists for the provider key).
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? UserInfoEndpoint { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
