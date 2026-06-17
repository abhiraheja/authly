using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.OAuth;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Applications;

/// <inheritdoc />
public sealed class ApplicationService : IApplicationService
{
    // Standard grant-type identifiers (match OpenIddict's values; kept as strings so this layer
    // stays free of OpenIddict types).
    private const string AuthorizationCode = "authorization_code";
    private const string RefreshToken = "refresh_token";
    private const string ClientCredentials = "client_credentials";

    private readonly IApplicationRepository _repo;
    private readonly IOAuthClientStore _clients;
    private readonly ICredentialGenerator _credentials;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _audit;

    public ApplicationService(
        IApplicationRepository repo,
        IOAuthClientStore clients,
        ICredentialGenerator credentials,
        IPasswordHasher hasher,
        IAuditLogger audit)
    {
        _repo = repo;
        _clients = clients;
        _credentials = credentials;
        _hasher = hasher;
        _audit = audit;
    }

    public async Task<ApplicationSecretResult> CreateAsync(Guid tenantId, CreateApplicationRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var isConfidential = request.Type is ApplicationType.Web or ApplicationType.Machine;
        var grantTypes = GrantTypesFor(request.Type);
        var scopes = NormalizeScopes(request.Type, request.Scopes);

        var clientId = _credentials.GenerateClientId();
        var rawSecret = isConfidential ? _credentials.GenerateClientSecret() : null;

        // 1) Register the protocol client in the OAuth server.
        await _clients.CreateClientAsync(new OAuthClientDescriptor(
            clientId, request.Name, rawSecret, isConfidential,
            grantTypes, request.RedirectUris, scopes, request.PostLogoutRedirectUris), ct);

        // 2) Persist the tenant-facing mirror.
        var now = DateTimeOffset.UtcNow;
        var app = new Application
        {
            TenantId = tenantId,
            ClientId = clientId,
            Name = request.Name,
            Type = request.Type,
            GrantTypes = grantTypes.ToList(),
            RedirectUris = request.RedirectUris.ToList(),
            PostLogoutRedirectUris = request.PostLogoutRedirectUris.ToList(),
            AllowedScopes = scopes.ToList(),
            CreatedAt = now,
            UpdatedAt = now
        };
        await _repo.AddAsync(app, ct);

        // 3) Record the secret (hashed) for our own audit/rotation trail.
        if (rawSecret is not null)
            await _repo.AddSecretAsync(new ApplicationSecret
            {
                ApplicationId = app.Id,
                SecretHash = _hasher.Hash(rawSecret),
                Label = "initial",
                CreatedAt = now
            }, ct);

        await _audit.LogAsync("application.created", actor, tenantId, "application", app.Id, ct: ct);
        return new ApplicationSecretResult(app, rawSecret);
    }

    public Task<IReadOnlyList<Application>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => _repo.ListByTenantAsync(tenantId, ct);

    public Task<Application?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _repo.GetByIdAsync(tenantId, id, ct);

    public async Task<Application> UpdateAsync(Guid tenantId, Guid id, UpdateApplicationRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdAsync(tenantId, id, ct) ?? throw new ApplicationNotFoundException(id);

        var scopes = NormalizeScopes(app.Type, request.Scopes);

        // 1) Update the protocol registration (name, redirect URIs, post-logout redirect URIs, scope
        //    permissions). The secret is preserved — the descriptor carries no secret on update.
        await _clients.UpdateClientAsync(new OAuthClientDescriptor(
            app.ClientId, request.Name, null, app.IsConfidential,
            app.GrantTypes, request.RedirectUris, scopes, request.PostLogoutRedirectUris), ct);

        // 2) Update the tenant-facing mirror.
        app.Name = request.Name;
        app.RedirectUris = request.RedirectUris.ToList();
        app.PostLogoutRedirectUris = request.PostLogoutRedirectUris.ToList();
        app.AllowedScopes = scopes.ToList();
        app.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(app, ct);

        await _audit.LogAsync("application.updated", actor, tenantId, "application", app.Id, ct: ct);
        return app;
    }

    public async Task<IReadOnlyList<ApplicationSecret>> ListSecretsAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdAsync(tenantId, id, ct) ?? throw new ApplicationNotFoundException(id);
        return await _repo.ListSecretsAsync(app.Id, ct);
    }

    public async Task<string> RotateSecretAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdAsync(tenantId, id, ct) ?? throw new ApplicationNotFoundException(id);
        if (!app.IsConfidential)
            throw new PublicClientHasNoSecretException();

        var rawSecret = _credentials.GenerateClientSecret();
        await _clients.SetClientSecretAsync(app.ClientId, rawSecret, ct);

        await _repo.RevokeSecretsAsync(app.Id, ct);
        await _repo.AddSecretAsync(new ApplicationSecret
        {
            ApplicationId = app.Id,
            SecretHash = _hasher.Hash(rawSecret),
            Label = "rotated",
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _audit.LogAsync("application.secret_rotated", actor, tenantId, "application", app.Id, ct: ct);
        return rawSecret;
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var app = await _repo.GetByIdAsync(tenantId, id, ct) ?? throw new ApplicationNotFoundException(id);
        await _clients.DeleteClientAsync(app.ClientId, ct);
        await _repo.DeleteAsync(app, ct);
        await _audit.LogAsync("application.deleted", actor, tenantId, "application", app.Id, ct: ct);
    }

    private static IReadOnlyList<string> GrantTypesFor(ApplicationType type) => type switch
    {
        ApplicationType.Machine => new[] { ClientCredentials },
        _ => new[] { AuthorizationCode, RefreshToken } // web/spa/native: interactive login + refresh
    };

    private static IReadOnlyList<string> NormalizeScopes(ApplicationType type, IReadOnlyList<string> requested)
    {
        var scopes = new HashSet<string>(requested, StringComparer.OrdinalIgnoreCase);
        if (type is not ApplicationType.Machine)
        {
            scopes.Add("openid");          // OIDC login
            scopes.Add("offline_access");  // enables refresh tokens
        }
        return scopes.ToList();
    }
}
