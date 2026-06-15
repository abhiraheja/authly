using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class MfaFactorRepository : IMfaFactorRepository
{
    private readonly AppDbContext _db;

    public MfaFactorRepository(AppDbContext db) => _db = db;

    public Task<MfaFactor?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.MfaFactors.FirstOrDefaultAsync(f => f.TenantId == tenantId && f.Id == id, ct);

    public async Task<IReadOnlyList<MfaFactor>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.MfaFactors
            .Where(f => f.TenantId == tenantId && f.UserId == userId)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<MfaFactor>> ListActiveByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.MfaFactors
            .Where(f => f.TenantId == tenantId && f.UserId == userId && f.Status == MfaFactorStatus.Active)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);

    public Task<MfaFactor?> GetActiveByTypeAsync(Guid tenantId, Guid userId, MfaFactorType type, CancellationToken ct = default)
        => _db.MfaFactors.FirstOrDefaultAsync(
            f => f.TenantId == tenantId && f.UserId == userId && f.Type == type && f.Status == MfaFactorStatus.Active, ct);

    public async Task<IReadOnlyList<MfaFactor>> ListActiveByTypeAsync(Guid tenantId, Guid userId, MfaFactorType type, CancellationToken ct = default)
        => await _db.MfaFactors
            .Where(f => f.TenantId == tenantId && f.UserId == userId && f.Type == type && f.Status == MfaFactorStatus.Active)
            .OrderBy(f => f.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> AnyActiveAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _db.MfaFactors.AnyAsync(
            f => f.TenantId == tenantId && f.UserId == userId && f.Status == MfaFactorStatus.Active, ct);

    public async Task AddAsync(MfaFactor factor, CancellationToken ct = default)
    {
        _db.MfaFactors.Add(factor);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MfaFactor factor, CancellationToken ct = default)
    {
        _db.MfaFactors.Update(factor);
        await _db.SaveChangesAsync(ct);
    }
}
