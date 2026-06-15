using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Security;

namespace Authly.Tests.Phase2;

public class ConditionalAccessTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Disabled_policy_always_allows()
    {
        var svc = Build(new TenantSecuritySettings { ConditionalAccessEnabled = false, NewDeviceAction = ConditionalAction.Block },
            out _, history: NewContext());
        var d = await svc.EvaluateAsync(Tenant, Verified(), Ctx("9.9.9.9", "newUA"));
        Assert.Equal(ConditionalAction.Allow, d.Action);
    }

    [Fact]
    public async Task New_device_triggers_configured_action()
    {
        var svc = Build(new TenantSecuritySettings { ConditionalAccessEnabled = true, NewDeviceAction = ConditionalAction.RequireMfa },
            out _, history: NewContext());
        var d = await svc.EvaluateAsync(Tenant, Verified(), Ctx("9.9.9.9", "brand-new-agent"));
        Assert.Equal(ConditionalAction.RequireMfa, d.Action);
        Assert.Equal("new_device", d.Reason);
    }

    [Fact]
    public async Task Known_device_does_not_trigger_new_device_rule()
    {
        // The current login plus a prior login from the same IP+UA → not a new context.
        var history = new List<LoginHistory>
        {
            Success("1.1.1.1", "agentA"),  // current
            Success("1.1.1.1", "agentA"),  // prior, same context
        };
        var svc = Build(new TenantSecuritySettings { ConditionalAccessEnabled = true, NewDeviceAction = ConditionalAction.Block },
            out _, history);
        var d = await svc.EvaluateAsync(Tenant, Verified(), Ctx("1.1.1.1", "agentA"));
        Assert.Equal(ConditionalAction.Allow, d.Action);
    }

    [Fact]
    public async Task Unverified_email_triggers_its_action()
    {
        var svc = Build(new TenantSecuritySettings { ConditionalAccessEnabled = true, UnverifiedEmailAction = ConditionalAction.RequireMfa },
            out _, history: new());
        var d = await svc.EvaluateAsync(Tenant, Unverified(), Ctx("1.1.1.1", "agentA"));
        Assert.Equal(ConditionalAction.RequireMfa, d.Action);
        Assert.Equal("unverified_email", d.Reason);
    }

    [Fact]
    public async Task Most_restrictive_signal_wins()
    {
        // New device → RequireMfa, unverified email → Block. Block must win.
        var svc = Build(new TenantSecuritySettings
        {
            ConditionalAccessEnabled = true,
            NewDeviceAction = ConditionalAction.RequireMfa,
            UnverifiedEmailAction = ConditionalAction.Block
        }, out _, history: NewContext());
        var d = await svc.EvaluateAsync(Tenant, Unverified(), Ctx("9.9.9.9", "fresh"));
        Assert.Equal(ConditionalAction.Block, d.Action);
        Assert.Equal("unverified_email", d.Reason);
    }

    [Fact]
    public async Task Verified_user_on_known_device_with_all_rules_is_allowed()
    {
        var history = new List<LoginHistory> { Success("1.1.1.1", "agentA"), Success("1.1.1.1", "agentA") };
        var svc = Build(new TenantSecuritySettings
        {
            ConditionalAccessEnabled = true,
            NewDeviceAction = ConditionalAction.Block,
            UnverifiedEmailAction = ConditionalAction.Block
        }, out _, history);
        var d = await svc.EvaluateAsync(Tenant, Verified(), Ctx("1.1.1.1", "agentA"));
        Assert.Equal(ConditionalAction.Allow, d.Action);
    }

    // --- helpers ---

    private static ConditionalAccessService Build(TenantSecuritySettings settings, out FakeSettings fake,
        List<LoginHistory> history, UserDevice? trustedDevice = null)
    {
        fake = new FakeSettings(settings);
        return new ConditionalAccessService(fake, new FakeHistory(history), new FakeDevices(trustedDevice));
    }

    [Fact]
    public async Task Trusted_device_suppresses_new_device_step_up()
    {
        // Same context the new-device test flags, but this fingerprint is a trusted device.
        var fingerprint = Authly.Modules.Devices.DeviceFingerprint.From("brand-new-agent");
        var trusted = new UserDevice { Fingerprint = fingerprint, Trusted = true };
        var svc = Build(new TenantSecuritySettings { ConditionalAccessEnabled = true, NewDeviceAction = ConditionalAction.RequireMfa },
            out _, history: NewContext(), trustedDevice: trusted);
        var d = await svc.EvaluateAsync(Tenant, Verified(), Ctx("9.9.9.9", "brand-new-agent"));
        Assert.Equal(ConditionalAction.Allow, d.Action);
    }

    private static List<LoginHistory> NewContext() => new()
    {
        Success("9.9.9.9", "fresh"),       // current (new ip+ua)
        Success("1.1.1.1", "agentA"),      // priors from a different context
        Success("1.1.1.1", "agentA"),
    };

    private static LoginHistory Success(string ip, string ua) =>
        new() { TenantId = Tenant, Result = "success", IpAddress = ip, UserAgent = ua, CreatedAt = DateTimeOffset.UtcNow };

    private static User Verified() => new() { Id = Guid.NewGuid(), TenantId = Tenant, Email = "a@x.com", EmailVerified = true };
    private static User Unverified() => new() { Id = Guid.NewGuid(), TenantId = Tenant, Email = "a@x.com", EmailVerified = false };
    private static RequestInfo Ctx(string ip, string ua) => new(ip, ua);
}

internal sealed class FakeSettings : ISecuritySettingsService
{
    private readonly TenantSecuritySettings _s;
    public FakeSettings(TenantSecuritySettings s) => _s = s;
    public Task<TenantSecuritySettings> GetAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(_s);
    public Task SaveAsync(Guid tenantId, TenantSecuritySettings settings, string? newCaptchaSecret, AuditContext actor, CancellationToken ct = default) => Task.CompletedTask;
    public string? DecryptCaptchaSecret(TenantSecuritySettings settings) => null;
}

internal sealed class FakeHistory : ILoginHistoryRepository
{
    private readonly List<LoginHistory> _items;
    public FakeHistory(List<LoginHistory> items) => _items = items;
    public Task AddAsync(LoginHistory entry, CancellationToken ct = default) { _items.Add(entry); return Task.CompletedTask; }
    public Task<IReadOnlyList<LoginHistory>> ListForUserAsync(Guid tenantId, Guid userId, int limit = 50, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<LoginHistory>>(_items.Take(limit).ToList());
}

internal sealed class FakeDevices : IUserDeviceRepository
{
    private readonly UserDevice? _device;
    public FakeDevices(UserDevice? device) => _device = device;
    public Task<UserDevice?> GetByFingerprintAsync(Guid tenantId, Guid userId, string fingerprint, CancellationToken ct = default)
        => Task.FromResult(_device is not null && _device.Fingerprint == fingerprint ? _device : null);

    // Unused by conditional-access tests.
    public Task<IReadOnlyList<UserDevice>> ListForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserDevice>>(Array.Empty<UserDevice>());
    public Task<UserDevice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => Task.FromResult<UserDevice?>(null);
    public Task AddAsync(UserDevice device, CancellationToken ct = default) => Task.CompletedTask;
    public Task UpdateAsync(UserDevice device, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(UserDevice device, CancellationToken ct = default) => Task.CompletedTask;
}
