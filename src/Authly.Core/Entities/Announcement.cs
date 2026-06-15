namespace Authly.Core.Entities;

/// <summary>
/// A platform-wide notice authored by a super admin and shown to tenant admins
/// (maintenance windows, version/deprecation notices, incidents). Platform-level —
/// NOT tenant-scoped, so it is exempt from RLS like <c>super_admins</c>.
/// </summary>
public class Announcement
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string Body { get; set; } = default!;

    /// <summary>Bootstrap contextual style: info | warning | danger | success.</summary>
    public string Severity { get; set; } = "info";

    /// <summary>When false, the notice is hidden regardless of dates.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Optional auto-expiry; null means it shows until deactivated.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Visible right now: active and not past its expiry.</summary>
    public bool IsVisible(DateTimeOffset now) => IsActive && (ExpiresAt is null || ExpiresAt > now);
}
