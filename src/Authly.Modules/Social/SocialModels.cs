using Authly.Core.Entities;

namespace Authly.Modules.Social;

/// <summary>A configured, active provider surfaced as a login button.</summary>
public sealed record SocialLoginOption(string Provider, string DisplayName);

/// <summary>Outcome of a completed social-login handshake.</summary>
/// <param name="User">The resolved (created or linked) user.</param>
/// <param name="Session">The session issued for the sign-in.</param>
/// <param name="IsNewUser">True when the user was just-in-time created.</param>
/// <param name="Linked">True when an existing email account was linked to this provider.</param>
public sealed record SocialLoginResult(User User, Session Session, bool IsNewUser, bool Linked);

/// <summary>Admin input for creating/updating a tenant's social provider. Blank secret = keep existing.</summary>
public sealed class SocialProviderInput
{
    public Guid? Id { get; set; }
    public string Provider { get; set; } = default!;
    public string ClientId { get; set; } = default!;
    public string? ClientSecret { get; set; }
    public string? Scopes { get; set; }
    public bool IsActive { get; set; } = true;

    // Generic OAuth2/OIDC endpoints (used when Provider == "custom").
    public string? AuthorizationEndpoint { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? UserInfoEndpoint { get; set; }
}

public sealed class SocialProviderNotConfiguredException : Exception
{
    public SocialProviderNotConfiguredException(string provider)
        : base($"Social provider '{provider}' is not configured or not active for this tenant.") { }
}

public sealed class SocialProfileMissingEmailException : Exception
{
    public SocialProfileMissingEmailException(string provider)
        : base($"The '{provider}' account did not return an email, which is required to sign in.") { }
}

public sealed class SocialProviderConfigInvalidException : Exception
{
    public SocialProviderConfigInvalidException(string message) : base(message) { }
}

/// <summary>
/// Raised when a social login resolves to a brand-new identity (no linked account and no
/// matching email) but the tenant has turned off self-service social sign-up. Existing accounts
/// are unaffected — only just-in-time creation of a new account is refused.
/// </summary>
public sealed class SocialSignupDisabledException : Exception
{
    public string Provider { get; }

    public SocialSignupDisabledException(string provider)
        : base($"Social sign-up is disabled for this tenant; no account exists for the '{provider}' identity.")
        => Provider = provider;
}
