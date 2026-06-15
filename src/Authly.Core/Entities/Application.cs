using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A tenant's OAuth client. This is the tenant-facing record (the source of truth for the
/// admin UI and Management API). The actual protocol registration lives in OpenIddict's own
/// client store, kept in sync via the same <c>ClientId</c>. Maps to table "applications".
/// </summary>
public class Application
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Public client identifier (<c>client_[24]</c>). Globally unique. Matches the OpenIddict client.</summary>
    public string ClientId { get; set; } = default!;

    public string Name { get; set; } = default!;

    public ApplicationType Type { get; set; }

    /// <summary>OAuth grant types this client may use (e.g. authorization_code, refresh_token, client_credentials).</summary>
    public List<string> GrantTypes { get; set; } = new();

    /// <summary>Allowed redirect URIs (authorization-code clients).</summary>
    public List<string> RedirectUris { get; set; } = new();

    /// <summary>Scopes this client is permitted to request.</summary>
    public List<string> AllowedScopes { get; set; } = new();

    /// <summary>Access-token lifetime in seconds.</summary>
    public int TokenLifetime { get; set; } = 3600;

    /// <summary>First-party clients owned by the tenant itself (e.g. the hosted login) skip consent.</summary>
    public bool IsFirstParty { get; set; }

    /// <summary>Arbitrary client configuration (JSONB).</summary>
    public string Settings { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<ApplicationSecret> Secrets { get; set; } = new List<ApplicationSecret>();

    /// <summary>Confidential clients (web/machine) authenticate with a secret; public clients (spa/native) use PKCE.</summary>
    public bool IsConfidential => Type is ApplicationType.Web or ApplicationType.Machine;
}
