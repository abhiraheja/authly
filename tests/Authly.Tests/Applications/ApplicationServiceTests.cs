using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.OAuth;
using Authly.Infrastructure.Security;
using Authly.Modules.Applications;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Tests.Applications;

public class ApplicationServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Create_confidential_client_returns_secret_registers_client_and_stores_hash()
    {
        var h = new Harness();
        var result = await h.Service.CreateAsync(Tenant,
            new CreateApplicationRequest("Web App", ApplicationType.Web,
                new[] { "https://app.example.com/callback" }, new[] { "openid", "email" }, Array.Empty<string>()),
            AuditContext.System);

        Assert.NotNull(result.ClientSecret);
        Assert.StartsWith("secret_", result.ClientSecret);
        Assert.StartsWith("client_", result.Application.ClientId);
        Assert.Single(h.Clients.Created);                                 // registered in OAuth server
        Assert.Equal(result.Application.ClientId, h.Clients.Created[0].ClientId);
        Assert.True(h.Clients.Created[0].IsConfidential);
        Assert.Contains("offline_access", result.Application.AllowedScopes); // auto-added for interactive
        Assert.Contains("openid", result.Application.AllowedScopes);

        var secret = Assert.Single(h.Repo.Secrets);
        Assert.NotEqual(result.ClientSecret, secret.SecretHash);          // stored hashed, not raw
        Assert.Contains("application.created", h.Audit.Events);
    }

    [Fact]
    public async Task Create_public_client_has_no_secret()
    {
        var h = new Harness();
        var result = await h.Service.CreateAsync(Tenant,
            new CreateApplicationRequest("SPA", ApplicationType.Spa,
                new[] { "https://spa.example.com/callback" }, new[] { "openid" }, Array.Empty<string>()),
            AuditContext.System);

        Assert.Null(result.ClientSecret);
        Assert.False(h.Clients.Created[0].IsConfidential);
        Assert.Empty(h.Repo.Secrets);
    }

    [Fact]
    public async Task Machine_client_uses_client_credentials_only()
    {
        var h = new Harness();
        var result = await h.Service.CreateAsync(Tenant,
            new CreateApplicationRequest("Service", ApplicationType.Machine,
                Array.Empty<string>(), new[] { "api" }, Array.Empty<string>()),
            AuditContext.System);

        Assert.Contains("client_credentials", result.Application.GrantTypes);
        Assert.DoesNotContain("authorization_code", result.Application.GrantTypes);
        Assert.DoesNotContain("openid", result.Application.AllowedScopes); // not added for machine clients
        Assert.NotNull(result.ClientSecret);
    }

    [Fact]
    public async Task Rotate_secret_revokes_old_and_sets_new_on_client()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(Tenant,
            new CreateApplicationRequest("Web App", ApplicationType.Web,
                new[] { "https://app.example.com/cb" }, new[] { "openid" }, Array.Empty<string>()),
            AuditContext.System);

        var newSecret = await h.Service.RotateSecretAsync(Tenant, created.Application.Id, AuditContext.System);

        Assert.StartsWith("secret_", newSecret);
        Assert.NotEqual(created.ClientSecret, newSecret);
        Assert.Equal(created.Application.ClientId, h.Clients.LastSecretClientId);
        // Exactly one active secret remains (the new one); the original is revoked.
        Assert.Single(h.Repo.Secrets, s => !s.Revoked);
        Assert.Contains("application.secret_rotated", h.Audit.Events);
    }

    [Fact]
    public async Task Rotate_secret_on_public_client_throws()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(Tenant,
            new CreateApplicationRequest("SPA", ApplicationType.Spa,
                new[] { "https://spa.example.com/cb" }, new[] { "openid" }, Array.Empty<string>()),
            AuditContext.System);

        await Assert.ThrowsAsync<PublicClientHasNoSecretException>(() =>
            h.Service.RotateSecretAsync(Tenant, created.Application.Id, AuditContext.System));
    }

    [Fact]
    public async Task Update_changes_redirect_uris_scopes_and_name_on_client_and_mirror()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(Tenant,
            new CreateApplicationRequest("Web App", ApplicationType.Web,
                new[] { "https://app.example.com/cb" }, new[] { "openid" }, Array.Empty<string>()),
            AuditContext.System);

        var updated = await h.Service.UpdateAsync(Tenant, created.Application.Id,
            new UpdateApplicationRequest("Renamed App",
                new[] { "https://app.example.com/cb", "https://staging.example.com/cb" },
                new[] { "email" },
                new[] { "https://app.example.com/" }),
            AuditContext.System);

        Assert.Equal("Renamed App", updated.Name);
        Assert.Equal(2, updated.RedirectUris.Count);
        Assert.Contains("https://staging.example.com/cb", updated.RedirectUris);
        Assert.Contains("https://app.example.com/", updated.PostLogoutRedirectUris);  // separate from callbacks
        Assert.Contains("email", updated.AllowedScopes);
        Assert.Contains("openid", updated.AllowedScopes);          // re-added for interactive clients
        Assert.Contains("offline_access", updated.AllowedScopes);

        var sentToClient = Assert.Single(h.Clients.Updated);        // protocol registration updated
        Assert.Equal(created.Application.ClientId, sentToClient.ClientId);
        Assert.Null(sentToClient.ClientSecret);                     // secret left untouched on update
        Assert.Contains("https://staging.example.com/cb", sentToClient.RedirectUris);
        Assert.Contains("https://app.example.com/", sentToClient.PostLogoutRedirectUris);
        Assert.Contains("application.updated", h.Audit.Events);
    }

    [Fact]
    public async Task Update_unknown_application_throws()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<ApplicationNotFoundException>(() =>
            h.Service.UpdateAsync(Tenant, Guid.NewGuid(),
                new UpdateApplicationRequest("X", Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>()),
                AuditContext.System));
    }

    [Fact]
    public async Task Delete_removes_client_and_mirror()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(Tenant,
            new CreateApplicationRequest("Web App", ApplicationType.Web,
                new[] { "https://app.example.com/cb" }, new[] { "openid" }, Array.Empty<string>()),
            AuditContext.System);

        await h.Service.DeleteAsync(Tenant, created.Application.Id, AuditContext.System);

        Assert.Contains(created.Application.ClientId, h.Clients.Deleted);
        Assert.Empty(h.Repo.Applications);
        Assert.Contains("application.deleted", h.Audit.Events);
    }

    private sealed class Harness
    {
        public readonly FakeApplicationRepository Repo = new();
        public readonly FakeClientStore Clients = new();
        public readonly RecordingAuditLogger Audit = new();
        public readonly ApplicationService Service;

        public Harness()
            => Service = new ApplicationService(Repo, Clients, new CredentialGenerator(),
                new Argon2idPasswordHasher(), Audit);
    }

    private sealed class FakeApplicationRepository : IApplicationRepository
    {
        public readonly List<Application> Applications = new();
        public readonly List<ApplicationSecret> Secrets = new();

        public Task<Application?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Applications.FirstOrDefault(a => a.TenantId == tenantId && a.Id == id));
        public Task<Application?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
            => Task.FromResult(Applications.FirstOrDefault(a => a.ClientId == clientId));
        public Task<IReadOnlyList<Application>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Application>>(Applications.Where(a => a.TenantId == tenantId).ToList());
        public Task<IReadOnlyList<string>> ListAllRedirectUrisAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Applications.SelectMany(a => a.RedirectUris).ToList());
        public Task AddAsync(Application application, CancellationToken ct = default)
        {
            if (application.Id == Guid.Empty) application.Id = Guid.NewGuid();
            Applications.Add(application);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(Application application, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Application application, CancellationToken ct = default)
        {
            Applications.Remove(application);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<ApplicationSecret>> ListSecretsAsync(Guid applicationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApplicationSecret>>(Secrets.Where(s => s.ApplicationId == applicationId).ToList());
        public Task AddSecretAsync(ApplicationSecret secret, CancellationToken ct = default)
        {
            if (secret.Id == Guid.Empty) secret.Id = Guid.NewGuid();
            Secrets.Add(secret);
            return Task.CompletedTask;
        }
        public Task RevokeSecretsAsync(Guid applicationId, CancellationToken ct = default)
        {
            foreach (var s in Secrets.Where(s => s.ApplicationId == applicationId && !s.Revoked)) s.Revoked = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClientStore : IOAuthClientStore
    {
        public readonly List<OAuthClientDescriptor> Created = new();
        public readonly List<OAuthClientDescriptor> Updated = new();
        public readonly List<string> Deleted = new();
        public string? LastSecretClientId;

        public Task CreateClientAsync(OAuthClientDescriptor descriptor, CancellationToken ct = default)
        {
            Created.Add(descriptor);
            return Task.CompletedTask;
        }
        public Task UpdateClientAsync(OAuthClientDescriptor descriptor, CancellationToken ct = default)
        {
            Updated.Add(descriptor);
            return Task.CompletedTask;
        }
        public Task DeleteClientAsync(string clientId, CancellationToken ct = default)
        {
            Deleted.Add(clientId);
            return Task.CompletedTask;
        }
        public Task SetClientSecretAsync(string clientId, string rawSecret, CancellationToken ct = default)
        {
            LastSecretClientId = clientId;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public readonly List<string> Events = new();
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null,
            string? resourceType = null, Guid? resourceId = null, string result = "success",
            object? metadata = null, bool publishEvent = true, CancellationToken ct = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }
}
