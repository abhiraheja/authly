using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class SessionRepository : ISessionRepository
{
    private readonly AppDbContext _db;

    public SessionRepository(AppDbContext db) => _db = db;

    public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Sessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default)
        => _db.Sessions.FirstOrDefaultAsync(s => s.RefreshTokenHash == refreshTokenHash, ct);

    public async Task<IReadOnlyList<Session>> ListActiveForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.Sessions
            .Where(s => s.TenantId == tenantId && s.UserId == userId && !s.Revoked && s.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(s => s.LastActiveAt)
            .ToListAsync(ct);

    public async Task AddAsync(Session session, CancellationToken ct = default)
    {
        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Session session, CancellationToken ct = default)
    {
        _db.Sessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }

    public Task<int> RevokeAllForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _db.Sessions
            .Where(s => s.TenantId == tenantId && s.UserId == userId && !s.Revoked)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct);
}
