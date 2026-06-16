using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

/// <summary>Tenant-scoped persistence for ABAC access policies (RLS backstop + explicit tenant filter).</summary>
public sealed class AccessPolicyRepository : IAccessPolicyRepository
{
    private readonly AppDbContext _db;

    public AccessPolicyRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<AccessPolicy>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.AccessPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId)
            .OrderByDescending(p => p.Priority).ThenBy(p => p.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AccessPolicy>> ListEnabledAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.AccessPolicies.AsNoTracking()
            .Where(p => p.TenantId == tenantId && p.Enabled)
            .ToListAsync(ct);

    public Task<AccessPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.AccessPolicies.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task AddAsync(AccessPolicy policy, CancellationToken ct = default)
    {
        _db.AccessPolicies.Add(policy);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AccessPolicy policy, CancellationToken ct = default)
    {
        _db.AccessPolicies.Update(policy);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(AccessPolicy policy, CancellationToken ct = default)
    {
        _db.AccessPolicies.Remove(policy);
        await _db.SaveChangesAsync(ct);
    }
}
