using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class RecoveryContactRepository : IRecoveryContactRepository
{
    private readonly AppDbContext _db;

    public RecoveryContactRepository(AppDbContext db) => _db = db;

    public Task<RecoveryContact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.RecoveryContacts.FirstOrDefaultAsync(c => c.TenantId == tenantId && c.Id == id, ct);

    public async Task<IReadOnlyList<RecoveryContact>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.RecoveryContacts
            .Where(c => c.TenantId == tenantId && c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(RecoveryContact contact, CancellationToken ct = default)
    {
        _db.RecoveryContacts.Add(contact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RecoveryContact contact, CancellationToken ct = default)
    {
        _db.RecoveryContacts.Update(contact);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(RecoveryContact contact, CancellationToken ct = default)
    {
        _db.RecoveryContacts.Remove(contact);
        await _db.SaveChangesAsync(ct);
    }
}
