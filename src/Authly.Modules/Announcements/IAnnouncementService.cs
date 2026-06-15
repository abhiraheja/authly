using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Announcements;

/// <summary>Super-admin authored platform notices, surfaced to tenant admins.</summary>
public interface IAnnouncementService
{
    Task<IReadOnlyList<Announcement>> ListAllAsync(CancellationToken ct = default);

    /// <summary>Notices to show right now (active and unexpired), newest first.</summary>
    Task<IReadOnlyList<Announcement>> ListVisibleAsync(CancellationToken ct = default);

    Task<Announcement?> GetAsync(Guid id, CancellationToken ct = default);
    Task<Announcement> CreateAsync(AnnouncementInput input, AuditContext actor, CancellationToken ct = default);
    Task UpdateAsync(Guid id, AnnouncementInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteAsync(Guid id, AuditContext actor, CancellationToken ct = default);
}

/// <summary>Editable fields for an announcement.</summary>
public sealed record AnnouncementInput(string Title, string Body, string Severity, bool IsActive, DateTimeOffset? ExpiresAt);
