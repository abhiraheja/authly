using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

/// <summary>Platform-level (not tenant-scoped) persistence for super-admin announcements.</summary>
public sealed class AnnouncementRepository : IAnnouncementRepository
{
    private readonly AppDbContext _db;

    public AnnouncementRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<Announcement>> ListAsync(CancellationToken ct = default)
        => await _db.Announcements.AsNoTracking().OrderByDescending(a => a.CreatedAt).ToListAsync(ct);

    public async Task<IReadOnlyList<Announcement>> ListVisibleAsync(DateTimeOffset now, CancellationToken ct = default)
        => await _db.Announcements.AsNoTracking()
            .Where(a => a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now))
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public Task<Announcement?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(Announcement announcement, CancellationToken ct = default)
    {
        _db.Announcements.Add(announcement);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Announcement announcement, CancellationToken ct = default)
    {
        _db.Announcements.Update(announcement);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Announcement announcement, CancellationToken ct = default)
    {
        _db.Announcements.Remove(announcement);
        await _db.SaveChangesAsync(ct);
    }
}
