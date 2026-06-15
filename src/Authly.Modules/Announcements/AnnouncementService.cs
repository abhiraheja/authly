using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Announcements;

/// <inheritdoc />
public sealed class AnnouncementService : IAnnouncementService
{
    private static readonly string[] AllowedSeverities = { "info", "warning", "danger", "success" };

    private readonly IAnnouncementRepository _repo;
    private readonly IAuditLogger _audit;

    public AnnouncementService(IAnnouncementRepository repo, IAuditLogger audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public Task<IReadOnlyList<Announcement>> ListAllAsync(CancellationToken ct = default) => _repo.ListAsync(ct);

    public Task<IReadOnlyList<Announcement>> ListVisibleAsync(CancellationToken ct = default)
        => _repo.ListVisibleAsync(DateTimeOffset.UtcNow, ct);

    public Task<Announcement?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);

    public async Task<Announcement> CreateAsync(AnnouncementInput input, AuditContext actor, CancellationToken ct = default)
    {
        var announcement = new Announcement
        {
            Title = input.Title.Trim(),
            Body = input.Body.Trim(),
            Severity = Normalize(input.Severity),
            IsActive = input.IsActive,
            ExpiresAt = input.ExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repo.AddAsync(announcement, ct);
        await _audit.LogAsync("announcement.created", actor, resourceType: "announcement", resourceId: announcement.Id,
            metadata: new { announcement.Title, announcement.Severity }, ct: ct);
        return announcement;
    }

    public async Task UpdateAsync(Guid id, AnnouncementInput input, AuditContext actor, CancellationToken ct = default)
    {
        var announcement = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Announcement {id} not found.");

        announcement.Title = input.Title.Trim();
        announcement.Body = input.Body.Trim();
        announcement.Severity = Normalize(input.Severity);
        announcement.IsActive = input.IsActive;
        announcement.ExpiresAt = input.ExpiresAt;
        await _repo.UpdateAsync(announcement, ct);

        await _audit.LogAsync("announcement.updated", actor, resourceType: "announcement", resourceId: announcement.Id, ct: ct);
    }

    public async Task DeleteAsync(Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var announcement = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Announcement {id} not found.");
        await _repo.DeleteAsync(announcement, ct);
        await _audit.LogAsync("announcement.deleted", actor, resourceType: "announcement", resourceId: id, ct: ct);
    }

    private static string Normalize(string severity)
    {
        var s = (severity ?? "").Trim().ToLowerInvariant();
        return AllowedSeverities.Contains(s) ? s : "info";
    }
}
