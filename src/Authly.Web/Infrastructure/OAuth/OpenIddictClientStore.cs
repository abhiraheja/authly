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
        await _manager.UpdateAsync(app, BuildDescriptor(descriptor), ct);
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

        return descriptor;
    }
}
