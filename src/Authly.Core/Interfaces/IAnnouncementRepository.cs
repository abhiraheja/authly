using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Platform-level store for super-admin announcements (no tenant scope / no RLS).</summary>
public interface IAnnouncementRepository
{
    Task<IReadOnlyList<Announcement>> ListAsync(CancellationToken ct = default);

    /// <summary>Currently-visible notices (active and unexpired), newest first.</summary>
    Task<IReadOnlyList<Announcement>> ListVisibleAsync(DateTimeOffset now, CancellationToken ct = default);

    Task<Announcement?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task AddAsync(Announcement announcement, CancellationToken ct = default);
    Task UpdateAsync(Announcement announcement, CancellationToken ct = default);
    Task DeleteAsync(Announcement announcement, CancellationToken ct = default);
}
