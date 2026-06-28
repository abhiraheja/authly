using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Tenants;

namespace Authly.Tests.Tenants;

public class TenantServiceTests
{
    [Theory]
    [InlineData("Acme Inc.", "acme-inc")]
    [InlineData("  Hello   World  ", "hello-world")]
    [InlineData("Café & Bar!!", "caf-bar")]
    [InlineData("ALREADY-Slug", "already-slug")]
    [InlineData("***", "tenant")]
    public void Slugify_normalizes_input(string input, string expected)
        => Assert.Equal(expected, TenantService.Slugify(input));

    [Fact]
    public async Task CreateAsync_derives_slug_persists_and_audits()
    {
        var repo = new FakeTenantRepository();
        var audit = new RecordingAuditLogger();
        var service = new TenantService(repo, audit);

        var orgId = Guid.NewGuid();
        var tenant = await service.CreateAsync(new CreateTenantRequest("Acme Inc.", OrganizationId: orgId),
            new AuditContext(Guid.NewGuid(), "super_admin"));

        Assert.Equal("acme-inc", tenant.Slug);
        Assert.Equal(orgId, tenant.OrganizationId);
        Assert.Equal(TenantStatus.Active, tenant.Status);
        Assert.Single(repo.Added);
        Assert.Equal("tenant.created", audit.Events.Single());
    }

    [Fact]
    public async Task CreateAsync_throws_on_slug_collision()
    {
        var repo = new FakeTenantRepository();
        repo.ExistingSlugs.Add("acme");
        var service = new TenantService(repo, new RecordingAuditLogger());

        await Assert.ThrowsAsync<SlugAlreadyExistsException>(() =>
            service.CreateAsync(new CreateTenantRequest("Acme", "acme", Guid.NewGuid()), AuditContext.System));
    }

    [Fact]
    public async Task DeleteAsync_soft_deletes_and_audits()
    {
        var repo = new FakeTenantRepository();
        var existing = new Tenant { Id = Guid.NewGuid(), Name = "Acme", Slug = "acme", Status = TenantStatus.Active };
        repo.Store[existing.Id] = existing;
        var audit = new RecordingAuditLogger();
        var service = new TenantService(repo, audit);

        await service.DeleteAsync(existing.Id, AuditContext.System);

        Assert.Equal(TenantStatus.Deleted, existing.Status);
        Assert.Equal("tenant.deleted", audit.Events.Single());
    }

    private sealed class FakeTenantRepository : ITenantRepository
    {
        public readonly Dictionary<Guid, Tenant> Store = new();
        public readonly List<Tenant> Added = new();
        public readonly HashSet<string> ExistingSlugs = new();

        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.GetValueOrDefault(id));
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Store.Values.FirstOrDefault(t => t.Slug == slug));
        public Task<Tenant?> GetByCustomDomainOrNullAsync(string host, CancellationToken ct = default)
            => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(Store.Values.ToList());
        public Task<IReadOnlyList<Tenant>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(Store.Values.Where(t => t.OrganizationId == organizationId).ToList());
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(ExistingSlugs.Contains(slug) || Store.Values.Any(t => t.Slug == slug));
        public Task AddAsync(Tenant tenant, CancellationToken ct = default)
        {
            Store[tenant.Id == Guid.Empty ? tenant.Id = Guid.NewGuid() : tenant.Id] = tenant;
            Added.Add(tenant);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default)
        {
            Store[tenant.Id] = tenant;
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
