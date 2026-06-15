namespace Authly.Core.Entities;

/// <summary>
/// A device a user has signed in from, identified by a stable fingerprint (currently derived from
/// the user-agent). Lets users name, trust, and forget their devices. A <em>trusted</em> device
/// suppresses the conditional-access "new device" step-up. Tenant-scoped (RLS).
/// </summary>
public class UserDevice
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>Stable, non-secret hash of the device's user-agent. Unique per (tenant, user).</summary>
    public string Fingerprint { get; set; } = default!;

    /// <summary>User-given name; defaults to a derived label (e.g. "Chrome on Windows").</summary>
    public string Label { get; set; } = default!;

    public string? UserAgent { get; set; }
    public string? LastIp { get; set; }

    /// <summary>When true, conditional-access treats sign-ins from this device as low-risk.</summary>
    public bool Trusted { get; set; }

    public DateTimeOffset FirstSeenAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}
