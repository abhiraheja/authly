namespace Authly.Core.OAuth;

/// <summary>
/// Protocol-level description of an OAuth client, used to create/update the registration in
/// the OAuth server (OpenIddict). Carries intent in neutral terms; the Infrastructure adapter
/// maps it to the server's permission model. Keeps the business layer free of OpenIddict types.
/// </summary>
/// <param name="ClientId">Public client identifier (<c>client_[24]</c>).</param>
/// <param name="DisplayName">Human-friendly application name.</param>
/// <param name="ClientSecret">Raw secret for confidential clients; null for public (PKCE) clients.</param>
/// <param name="IsConfidential">True for web/machine clients that authenticate with a secret.</param>
/// <param name="GrantTypes">e.g. authorization_code, refresh_token, client_credentials.</param>
/// <param name="RedirectUris">Allowed redirect URIs (authorization-code clients).</param>
/// <param name="Scopes">Scopes the client may request (openid, profile, email, offline_access, roles, ...).</param>
/// <param name="PostLogoutRedirectUris">Allowed post-logout redirect URIs (RP-initiated end-session).</param>
public sealed record OAuthClientDescriptor(
    string ClientId,
    string DisplayName,
    string? ClientSecret,
    bool IsConfidential,
    IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> PostLogoutRedirectUris);
