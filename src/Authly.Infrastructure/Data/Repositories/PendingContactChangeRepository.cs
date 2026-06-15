using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class PendingContactChangeRepository : IPendingContactChangeRepository
{
    private readonly AppDbContext _db;

    public PendingContactChangeRepository(AppDbContext db) => _db = db;

    public Task<PendingContactChange?> GetPendingByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _db.PendingContactChanges.FirstOrDefaultAsync(
            c => c.TenantId == tenantId && c.UserId == userId && c.Status == ContactChangeStatus.Pending, ct);

    // Looked up by unique token hash; the row carries tenant_id (RLS still applies when set).
    public Task<PendingContactChange?> GetByVerifyHashAsync(string verifyTokenHash, CancellationToken ct = default)
        => _db.PendingContactChanges.FirstOrDefaultAsync(c => c.VerifyTokenHash == verifyTokenHash, ct);

    public Task<PendingContactChange?> GetByCancelHashAsync(string cancelTokenHash, CancellationToken ct = default)
        => _db.PendingContactChanges.FirstOrDefaultAsync(c => c.CancelTokenHash == cancelTokenHash, ct);

    public async Task AddAsync(PendingContactChange change, CancellationToken ct = default)
    {
        _db.PendingContactChanges.Add(change);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PendingContactChange change, CancellationToken ct = default)
    {
        _db.PendingContactChanges.Update(change);
        await _db.SaveChangesAsync(ct);
    }
}
