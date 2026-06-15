namespace Authly.Core.OAuth;

/// <summary>
/// Everything the OAuth2 gateway needs to drive an authorization-code flow for one provider:
/// resolved client credentials, scopes, and endpoints. Built by the module layer from the
/// tenant's stored config merged with the provider preset.
/// </summary>
public sealed record SocialAuthConfig(
    string Provider,
    string ClientId,
    string ClientSecret,
    IReadOnlyList<string> Scopes,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string UserInfoEndpoint);

/// <summary>Tokens returned by a provider's token endpoint.</summary>
public sealed record SocialTokenSet(
    string AccessToken,
    string? RefreshToken,
    DateTimeOffset? ExpiresAt,
    string? IdToken);

/// <summary>
/// The OAuth2/OIDC transport: builds the authorization URL, exchanges the code for tokens, and
/// fetches the raw user-info JSON. Implemented in Infrastructure (HttpClient); provider-specific
/// field mapping is done in the module layer from the raw JSON.
/// </summary>
public interface ISocialAuthGateway
{
    string BuildAuthorizationUrl(SocialAuthConfig config, string redirectUri, string state);

    Task<SocialTokenSet> ExchangeCodeAsync(SocialAuthConfig config, string code, string redirectUri, CancellationToken ct = default);

    Task<string> FetchUserInfoJsonAsync(SocialAuthConfig config, SocialTokenSet tokens, CancellationToken ct = default);
}

/// <summary>Raised when a provider handshake fails (bad code, network, malformed response).</summary>
public sealed class SocialAuthException : Exception
{
    public SocialAuthException(string message) : base(message) { }
}
