using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class ClaimConfigRepository : IClaimConfigRepository
{
    private readonly AppDbContext _db;

    public ClaimConfigRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ClaimConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.ClaimConfigs
            .Where(c => c.TenantId == tenantId)
            .OrderBy(c => c.ClaimName)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ClaimConfig>> ListForIssuanceAsync(Guid tenantId, Guid? applicationId, CancellationToken ct = default)
        // Tenant-wide rows (application_id null) plus rows targeting the issuing application.
        => await _db.ClaimConfigs
            .Where(c => c.TenantId == tenantId
                && (c.ApplicationId == null || c.ApplicationId == applicationId))
            .ToListAsync(ct);

    public Task<ClaimConfig?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.ClaimConfigs.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public async Task AddAsync(ClaimConfig config, CancellationToken ct = default)
    {
        _db.ClaimConfigs.Add(config);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ClaimConfig config, CancellationToken ct = default)
    {
        _db.ClaimConfigs.Remove(config);
        await _db.SaveChangesAsync(ct);
    }
}
