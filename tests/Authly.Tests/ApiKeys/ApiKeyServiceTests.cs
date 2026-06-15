using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Infrastructure.Security;
using Authly.Modules.ApiKeys;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Tests.ApiKeys;

public class ApiKeyServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Create_returns_raw_key_once_and_stores_only_the_hash()
    {
        var h = new Harness();
        var result = await h.Service.CreateAsync(Tenant,
            new CreateApiKeyRequest("CI key", new[] { "user.read" }), AuditContext.System);

        Assert.StartsWith("authly_sk_", result.RawKey);
        var stored = Assert.Single(h.Repo.Items);
        Assert.NotEqual(result.RawKey, stored.KeyHash);                 // stored hashed, not raw
        Assert.Equal(new Sha256TokenHasher().Hash(result.RawKey), stored.KeyHash);
        Assert.Equal(new[] { "user.read" }, stored.Scopes);
        Assert.Contains("api_key.created", h.Audit.Events);
    }

    [Fact]
    public async Task Create_with_no_scopes_defaults_to_full_access()
    {
        var h = new Harness();
        var result = await h.Service.CreateAsync(Tenant,
            new CreateApiKeyRequest("root", Array.Empty<string>()), AuditContext.System);
        Assert.Equal(new[] { "*" }, result.Key.Scopes);
    }

    [Fact]
    public async Task Revoke_marks_key_revoked()
    {
        var h = new Harness();
        var created = await h.Service.CreateAsync(Tenant,
            new CreateApiKeyRequest("k", new[] { "*" }), AuditContext.System);

        await h.Service.RevokeAsync(Tenant, created.Key.Id, AuditContext.System);

        Assert.True(h.Repo.Items.Single().Revoked);
        Assert.Contains("api_key.revoked", h.Audit.Events);
    }

    [Fact]
    public async Task Revoke_unknown_key_throws()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<ApiKeyNotFoundException>(() =>
            h.Service.RevokeAsync(Tenant, Guid.NewGuid(), AuditContext.System));
    }

    private sealed class Harness
    {
        public readonly FakeApiKeyRepository Repo = new();
        public readonly RecordingAuditLogger Audit = new();
        public readonly ApiKeyService Service;

        public Harness() => Service = new ApiKeyService(Repo, new CredentialGenerator(), new Sha256TokenHasher(), Audit);
    }

    private sealed class FakeApiKeyRepository : IApiKeyRepository
    {
        public readonly List<ApiKey> Items = new();

        public Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(k => k.KeyHash == keyHash));
        public Task<IReadOnlyList<ApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ApiKey>>(Items.Where(k => k.TenantId == tenantId).ToList());
        public Task<ApiKey?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(k => k.TenantId == tenantId && k.Id == id));
        public Task AddAsync(ApiKey key, CancellationToken ct = default)
        {
            if (key.Id == Guid.Empty) key.Id = Guid.NewGuid();
            Items.Add(key);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(ApiKey key, CancellationToken ct = default) => Task.CompletedTask;
        public Task TouchLastUsedAsync(Guid id, DateTimeOffset whenUtc, CancellationToken ct = default)
        {
            var k = Items.FirstOrDefault(x => x.Id == id);
            if (k is not null) k.LastUsedAt = whenUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public readonly List<string> Events = new();
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null,
            string? resourceType = null, Guid? resourceId = null, string result = "success",
            object? metadata = null, CancellationToken ct = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }
}
