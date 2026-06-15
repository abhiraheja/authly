using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Devices;

/// <summary>Result of recording a login's device: the device row and whether it was newly seen.</summary>
public sealed record DeviceLogin(UserDevice Device, bool IsNew);

/// <summary>
/// Manages a user's known devices: records them on sign-in, and lets the user (or admin) trust,
/// rename, and forget them. Tenant-scoped throughout.
/// </summary>
public interface IDeviceService
{
    /// <summary>Upserts the device behind this sign-in (best-effort), bumping last-seen. Returns whether it was new.</summary>
    Task<DeviceLogin> RecordLoginAsync(Guid tenantId, Guid userId, RequestInfo info, CancellationToken ct = default);

    Task<IReadOnlyList<UserDevice>> ListAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<bool> IsTrustedAsync(Guid tenantId, Guid userId, string fingerprint, CancellationToken ct = default);
    Task SetTrustedAsync(Guid tenantId, Guid userId, Guid deviceId, bool trusted, AuditContext actor, CancellationToken ct = default);
    Task RenameAsync(Guid tenantId, Guid userId, Guid deviceId, string label, AuditContext actor, CancellationToken ct = default);
    Task ForgetAsync(Guid tenantId, Guid userId, Guid deviceId, AuditContext actor, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class DeviceService : IDeviceService
{
    private readonly IUserDeviceRepository _repo;
    private readonly IAuditLogger _audit;

    public DeviceService(IUserDeviceRepository repo, IAuditLogger audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public async Task<DeviceLogin> RecordLoginAsync(Guid tenantId, Guid userId, RequestInfo info, CancellationToken ct = default)
    {
        var fingerprint = DeviceFingerprint.From(info.UserAgent);
        var now = DateTimeOffset.UtcNow;
        var existing = await _repo.GetByFingerprintAsync(tenantId, userId, fingerprint, ct);
        if (existing is not null)
        {
            existing.LastSeenAt = now;
            existing.LastIp = info.IpAddress;
            await _repo.UpdateAsync(existing, ct);
            return new DeviceLogin(existing, IsNew: false);
        }

        var device = new UserDevice
        {
            TenantId = tenantId,
            UserId = userId,
            Fingerprint = fingerprint,
            Label = DeviceFingerprint.Label(info.UserAgent),
            UserAgent = info.UserAgent,
            LastIp = info.IpAddress,
            Trusted = false,
            FirstSeenAt = now,
            LastSeenAt = now
        };
        await _repo.AddAsync(device, ct);
        await _audit.LogAsync("user.device_seen", new AuditContext(userId, "user", info.IpAddress, info.UserAgent),
            tenantId, "user_device", device.Id, metadata: new { @new = true }, ct: ct);
        return new DeviceLogin(device, IsNew: true);
    }

    public Task<IReadOnlyList<UserDevice>> ListAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _repo.ListForUserAsync(tenantId, userId, ct);

    public async Task<bool> IsTrustedAsync(Guid tenantId, Guid userId, string fingerprint, CancellationToken ct = default)
    {
        var device = await _repo.GetByFingerprintAsync(tenantId, userId, fingerprint, ct);
        return device is { Trusted: true };
    }

    public async Task SetTrustedAsync(Guid tenantId, Guid userId, Guid deviceId, bool trusted, AuditContext actor, CancellationToken ct = default)
    {
        var device = await Owned(tenantId, userId, deviceId, ct);
        device.Trusted = trusted;
        await _repo.UpdateAsync(device, ct);
        await _audit.LogAsync(trusted ? "user.device_trusted" : "user.device_untrusted", actor,
            tenantId, "user_device", deviceId, ct: ct);
    }

    public async Task RenameAsync(Guid tenantId, Guid userId, Guid deviceId, string label, AuditContext actor, CancellationToken ct = default)
    {
        var device = await Owned(tenantId, userId, deviceId, ct);
        var trimmed = (label ?? "").Trim();
        if (trimmed.Length is 0 or > 80) throw new ArgumentException("Device name must be 1–80 characters.", nameof(label));
        device.Label = trimmed;
        await _repo.UpdateAsync(device, ct);
        await _audit.LogAsync("user.device_renamed", actor, tenantId, "user_device", deviceId, ct: ct);
    }

    public async Task ForgetAsync(Guid tenantId, Guid userId, Guid deviceId, AuditContext actor, CancellationToken ct = default)
    {
        var device = await Owned(tenantId, userId, deviceId, ct);
        await _repo.DeleteAsync(device, ct);
        await _audit.LogAsync("user.device_forgotten", actor, tenantId, "user_device", deviceId, ct: ct);
    }

    // Enforces tenant + ownership before any mutation (defence in depth alongside RLS).
    private async Task<UserDevice> Owned(Guid tenantId, Guid userId, Guid deviceId, CancellationToken ct)
    {
        var device = await _repo.GetByIdAsync(tenantId, deviceId, ct);
        if (device is null || device.UserId != userId)
            throw new KeyNotFoundException($"Device {deviceId} not found for this user.");
        return device;
    }
}
