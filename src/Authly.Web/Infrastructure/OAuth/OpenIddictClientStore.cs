using Authly.Core.Interfaces;
using Authly.Core.OAuth;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Authly.Web.Infrastructure.OAuth;

/// <summary>
/// <see cref="IOAuthClientStore"/> over OpenIddict's application manager. Translates the neutral
/// <see cref="OAuthClientDescriptor"/> into OpenIddict's permission/requirement model so the
/// business layer never references OpenIddict types.
/// </summary>
public sealed class OpenIddictClientStore : IOAuthClientStore
{
    private readonly IOpenIddictApplicationManager _manager;

    public OpenIddictClientStore(IOpenIddictApplicationManager manager) => _manager = manager;

    public async Task CreateClientAsync(OAuthClientDescriptor descriptor, CancellationToken ct = default)
    {
        if (await _manager.FindByClientIdAsync(descriptor.ClientId, ct) is not null)
            return; // already registered

        await _manager.CreateAsync(BuildDescriptor(descriptor), ct);
    }

    public async Task UpdateClientAsync(OAuthClientDescriptor descriptor, CancellationToken ct = default)
    {
        var app = await _manager.FindByClientIdAsync(descriptor.ClientId, ct)
            ?? throw new InvalidOperationException($"OAuth client '{descriptor.ClientId}' not found.");

        // Populate from the existing registration so the (hashed) secret, grant/endpoint
        // permissions and requirements are preserved — then overwrite only the editable fields.
        // OpenIddict's UpdateAsync re-hashes the secret only if it changed, so leaving the
        // populated value untouched keeps the secret intact.
        var d = new OpenIddictApplicationDescriptor();
        await _manager.PopulateAsync(d, app, ct);

        d.DisplayName = descriptor.DisplayName;

        // Replace scope permissions, leaving endpoint/grant/response permissions in place.
        foreach (var p in d.Permissions
                     .Where(p => p.StartsWith(Permissions.Prefixes.Scope, StringComparison.Ordinal))
                     .ToList())
            d.Permissions.Remove(p);
        foreach (var scope in descriptor.Scopes)
        {
            if (scope.Equals(Scopes.OpenId, StringComparison.OrdinalIgnoreCase) ||
                scope.Equals(Scopes.OfflineAccess, StringComparison.OrdinalIgnoreCase))
                continue;
            d.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        // Replace redirect URIs.
        d.RedirectUris.Clear();
        foreach (var uri in descriptor.RedirectUris)
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                d.RedirectUris.Add(parsed);

        // Replace post-logout redirect URIs (explicit, separate from the callback URIs).
        d.PostLogoutRedirectUris.Clear();
        foreach (var uri in descriptor.PostLogoutRedirectUris)
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                d.PostLogoutRedirectUris.Add(parsed);

        await _manager.UpdateAsync(app, d, ct);
    }

    public async Task DeleteClientAsync(string clientId, CancellationToken ct = default)
    {
        var app = await _manager.FindByClientIdAsync(clientId, ct);
        if (app is not null)
            await _manager.DeleteAsync(app, ct);
    }

    public async Task SetClientSecretAsync(string clientId, string rawSecret, CancellationToken ct = default)
    {
        var app = await _manager.FindByClientIdAsync(clientId, ct)
            ?? throw new InvalidOperationException($"OAuth client '{clientId}' not found.");

        var descriptor = new OpenIddictApplicationDescriptor();
        await _manager.PopulateAsync(descriptor, app, ct);
        descriptor.ClientSecret = rawSecret; // OpenIddict hashes it on update
        await _manager.UpdateAsync(app, descriptor, ct);
    }

    private static OpenIddictApplicationDescriptor BuildDescriptor(OAuthClientDescriptor d)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = d.ClientId,
            DisplayName = d.DisplayName,
            ClientType = d.IsConfidential ? ClientTypes.Confidential : ClientTypes.Public,
            // No consent screen is built yet; first-party clients sign in directly.
            ConsentType = ConsentTypes.Implicit
        };

        if (d.IsConfidential && !string.IsNullOrEmpty(d.ClientSecret))
            descriptor.ClientSecret = d.ClientSecret;

        // Token endpoint is needed by every grant.
        descriptor.Permissions.Add(Permissions.Endpoints.Token);

        var grants = new HashSet<string>(d.GrantTypes, StringComparer.OrdinalIgnoreCase);

        if (grants.Contains(GrantTypes.AuthorizationCode))
        {
            descriptor.Permissions.Add(Permissions.Endpoints.Authorization);
            descriptor.Permissions.Add(Permissions.GrantTypes.AuthorizationCode);
            descriptor.Permissions.Add(Permissions.ResponseTypes.Code);
            descriptor.Permissions.Add(Permissions.Endpoints.EndSession);
            // PKCE is mandatory for the code flow.
            descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        }

        if (grants.Contains(GrantTypes.RefreshToken))
            descriptor.Permissions.Add(Permissions.GrantTypes.RefreshToken);

        if (grants.Contains(GrantTypes.ClientCredentials))
            descriptor.Permissions.Add(Permissions.GrantTypes.ClientCredentials);

        // Confidential clients may introspect/revoke their own tokens.
        if (d.IsConfidential)
        {
            descriptor.Permissions.Add(Permissions.Endpoints.Introspection);
            descriptor.Permissions.Add(Permissions.Endpoints.Revocation);
        }

        foreach (var scope in d.Scopes)
        {
            // openid/offline_access are governed by flow/grant, not scope permissions.
            if (scope.Equals(Scopes.OpenId, StringComparison.OrdinalIgnoreCase) ||
                scope.Equals(Scopes.OfflineAccess, StringComparison.OrdinalIgnoreCase))
                continue;
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        foreach (var uri in d.RedirectUris)
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                descriptor.RedirectUris.Add(parsed);

        // RP-initiated logout (end-session): the client's own, explicitly-configured post-logout
        // redirect URIs (kept separate from the OAuth callback URIs).
        foreach (var uri in d.PostLogoutRedirectUris)
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
                descriptor.PostLogoutRedirectUris.Add(parsed);

        return descriptor;
    }
}
