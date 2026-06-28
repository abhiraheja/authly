using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Tests.Policies;

internal sealed class FakePolicyRepo : IPolicyRepository
{
    public readonly List<Policy> Policies = new();
    public readonly List<PolicyVersion> Versions = new();
    public readonly List<PolicyAsset> Assets = new();
    public readonly List<PolicyDecision> Decisions = new();

    public Task<IReadOnlyList<Policy>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Policy>>(Policies.Where(p => p.TenantId == tenantId).ToList());

    public Task<IReadOnlyList<Policy>> ListPublishedAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Policy>>(
            Policies.Where(p => p.TenantId == tenantId && p.Status == Core.Enums.PolicyStatus.Published).ToList());

    public Task<Policy?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult(Policies.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));

    public Task AddAsync(Policy policy, CancellationToken ct = default) { Policies.Add(policy); return Task.CompletedTask; }
    public Task UpdateAsync(Policy policy, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(Policy policy, CancellationToken ct = default) { Policies.Remove(policy); return Task.CompletedTask; }

    public Task<IReadOnlyList<PolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PolicyVersion>>(Versions.Where(v => v.PolicyId == policyId).ToList());

    public Task<PolicyVersion?> GetVersionAsync(Guid tenantId, Guid versionId, CancellationToken ct = default)
        => Task.FromResult(Versions.FirstOrDefault(v => v.TenantId == tenantId && v.Id == versionId));

    public Task<int> NextVersionNumberAsync(Guid policyId, CancellationToken ct = default)
        => Task.FromResult((Versions.Where(v => v.PolicyId == policyId).Select(v => (int?)v.Version).Max() ?? 0) + 1);

    public Task AddVersionAsync(PolicyVersion version, CancellationToken ct = default) { Versions.Add(version); return Task.CompletedTask; }

    public Task<PolicyAsset?> GetAssetAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Assets.FirstOrDefault(a => a.Id == id));
    public Task AddAssetAsync(PolicyAsset asset, CancellationToken ct = default) { Assets.Add(asset); return Task.CompletedTask; }

    public Task<IReadOnlyList<PolicyDecision>> ListDecisionsForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PolicyDecision>>(
            Decisions.Where(d => d.TenantId == tenantId && d.UserId == userId).ToList());

    public Task<IReadOnlyList<PolicyDecision>> ListDecisionsForPolicyAsync(Guid tenantId, Guid policyId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<PolicyDecision>>(
            Decisions.Where(d => d.TenantId == tenantId && d.PolicyId == policyId).ToList());

    public Task AddDecisionAsync(PolicyDecision decision, CancellationToken ct = default) { Decisions.Add(decision); return Task.CompletedTask; }
}

internal sealed class FakeLoginHistoryRepo : ILoginHistoryRepository
{
    public readonly List<LoginHistory> Items = new();
    public Task AddAsync(LoginHistory entry, CancellationToken ct = default) { Items.Add(entry); return Task.CompletedTask; }
    public Task<IReadOnlyList<LoginHistory>> ListForUserAsync(Guid tenantId, Guid userId, int limit = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LoginHistory>>(
            Items.Where(h => h.TenantId == tenantId && h.UserId == userId).ToList());
}

internal sealed class FakeSocialIdentityRepo : ISocialIdentityRepository
{
    public readonly List<SocialIdentity> Items = new();
    public Task<SocialIdentity?> GetAsync(Guid tenantId, string provider, string providerId, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == tenantId && s.Provider == provider && s.ProviderId == providerId));
    public Task<IReadOnlyList<SocialIdentity>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SocialIdentity>>(Items.Where(s => s.TenantId == tenantId && s.UserId == userId).ToList());
    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> ListProvidersByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<string>>>(
            Items.Where(s => s.TenantId == tenantId)
                .GroupBy(s => s.UserId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(s => s.Provider).ToList()));
    public Task AddAsync(SocialIdentity identity, CancellationToken ct = default) { Items.Add(identity); return Task.CompletedTask; }
    public Task UpdateAsync(SocialIdentity identity, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeUserRoleRepo : IUserRoleRepository
{
    /// <summary>tenant+user → role names.</summary>
    public readonly Dictionary<(Guid, Guid), List<string>> RolesByUser = new();
    /// <summary>tenant+roleId → user ids.</summary>
    public readonly Dictionary<(Guid, Guid), List<Guid>> UsersByRole = new();

    public Task AssignAsync(UserRole assignment, CancellationToken ct = default) => Task.CompletedTask;
    public Task RemoveAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<Role>> ListRolesForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Role>>(Array.Empty<Role>());
    public Task<IReadOnlyList<Guid>> ListUserIdsForRoleAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(UsersByRole.TryGetValue((tenantId, roleId), out var v) ? v : new List<Guid>());
    public Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(RolesByUser.TryGetValue((tenantId, userId), out var v) ? v : new List<string>());
    public Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}

internal sealed class NoopAudit : IAuditLogger
{
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, bool publishEvent = true, CancellationToken ct = default)
        => Task.CompletedTask;
}
