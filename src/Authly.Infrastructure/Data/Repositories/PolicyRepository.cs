using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class PolicyRepository : IPolicyRepository
{
    private readonly AppDbContext _db;

    public PolicyRepository(AppDbContext db) => _db = db;

    // --- Policies ---

    public async Task<IReadOnlyList<Policy>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Policies
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Policy>> ListPublishedAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Policies
            .Where(p => p.TenantId == tenantId && p.Status == PolicyStatus.Published)
            .ToListAsync(ct);

    public Task<Policy?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Policies.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task AddAsync(Policy policy, CancellationToken ct = default)
    {
        _db.Policies.Add(policy);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Policy policy, CancellationToken ct = default)
    {
        _db.Policies.Update(policy);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Policy policy, CancellationToken ct = default)
    {
        _db.Policies.Remove(policy);
        await _db.SaveChangesAsync(ct);
    }

    // --- Versions ---

    public async Task<IReadOnlyList<PolicyVersion>> ListVersionsAsync(Guid policyId, CancellationToken ct = default)
        => await _db.PolicyVersions
            .Where(v => v.PolicyId == policyId)
            .OrderByDescending(v => v.Version)
            .ToListAsync(ct);

    public Task<PolicyVersion?> GetVersionAsync(Guid tenantId, Guid versionId, CancellationToken ct = default)
        => _db.PolicyVersions.FirstOrDefaultAsync(v => v.TenantId == tenantId && v.Id == versionId, ct);

    public async Task<int> NextVersionNumberAsync(Guid policyId, CancellationToken ct = default)
    {
        var max = await _db.PolicyVersions
            .Where(v => v.PolicyId == policyId)
            .Select(v => (int?)v.Version)
            .MaxAsync(ct);
        return (max ?? 0) + 1;
    }

    public async Task AddVersionAsync(PolicyVersion version, CancellationToken ct = default)
    {
        _db.PolicyVersions.Add(version);
        await _db.SaveChangesAsync(ct);
    }

    // --- Assets ---

    public Task<PolicyAsset?> GetAssetAsync(Guid id, CancellationToken ct = default)
        => _db.PolicyAssets.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAssetAsync(PolicyAsset asset, CancellationToken ct = default)
    {
        _db.PolicyAssets.Add(asset);
        await _db.SaveChangesAsync(ct);
    }

    // --- Decisions ---

    public async Task<IReadOnlyList<PolicyDecision>> ListDecisionsForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.PolicyDecisions
            .Where(d => d.TenantId == tenantId && d.UserId == userId)
            .OrderByDescending(d => d.DecidedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PolicyDecision>> ListDecisionsForPolicyAsync(Guid tenantId, Guid policyId, CancellationToken ct = default)
        => await _db.PolicyDecisions
            .Where(d => d.TenantId == tenantId && d.PolicyId == policyId)
            .OrderByDescending(d => d.DecidedAt)
            .ToListAsync(ct);

    public async Task AddDecisionAsync(PolicyDecision decision, CancellationToken ct = default)
    {
        _db.PolicyDecisions.Add(decision);
        await _db.SaveChangesAsync(ct);
    }
}
