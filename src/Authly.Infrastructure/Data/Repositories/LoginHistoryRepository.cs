using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class LoginHistoryRepository : ILoginHistoryRepository
{
    private readonly AppDbContext _db;

    public LoginHistoryRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(LoginHistory entry, CancellationToken ct = default)
    {
        _db.LoginHistory.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<LoginHistory>> ListForUserAsync(Guid tenantId, Guid userId, int limit = 50, CancellationToken ct = default)
        => await _db.LoginHistory
            .Where(h => h.TenantId == tenantId && h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);
}
