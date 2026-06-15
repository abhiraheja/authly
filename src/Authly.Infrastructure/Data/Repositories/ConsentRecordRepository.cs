using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class ConsentRecordRepository : IConsentRecordRepository
{
    private readonly AppDbContext _db;

    public ConsentRecordRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConsentRecord>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.ConsentRecords
            .Where(c => c.TenantId == tenantId && c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(ConsentRecord record, CancellationToken ct = default)
    {
        _db.ConsentRecords.Add(record);
        await _db.SaveChangesAsync(ct);
    }
}
