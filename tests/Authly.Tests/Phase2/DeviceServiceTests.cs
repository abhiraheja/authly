using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Devices;

namespace Authly.Tests.Phase2;

public class DeviceServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public void Fingerprint_is_stable_and_distinguishes_user_agents()
    {
        Assert.Equal(DeviceFingerprint.From("UA-1"), DeviceFingerprint.From("UA-1"));
        Assert.NotEqual(DeviceFingerprint.From("UA-1"), DeviceFingerprint.From("UA-2"));
        Assert.Equal("unknown", DeviceFingerprint.From(null));
    }

    [Fact]
    public async Task First_login_records_a_new_untrusted_device()
    {
        var (svc, repo, _) = Build();
        var r = await svc.RecordLoginAsync(Tenant, UserId, new RequestInfo("1.1.1.1", "Mozilla Chrome Windows"));
        Assert.True(r.IsNew);
        Assert.False(r.Device.Trusted);
        Assert.Single(repo.Items);
        Assert.Contains("Windows", r.Device.Label);
    }

    [Fact]
    public async Task Repeat_login_updates_existing_device_not_a_new_one()
    {
        var (svc, repo, _) = Build();
        await svc.RecordLoginAsync(Tenant, UserId, new RequestInfo("1.1.1.1", "UA"));
        var second = await svc.RecordLoginAsync(Tenant, UserId, new RequestInfo("2.2.2.2", "UA"));
        Assert.False(second.IsNew);
        Assert.Single(repo.Items);
        Assert.Equal("2.2.2.2", repo.Items[0].LastIp); // last-seen IP updated
    }

    [Fact]
    public async Task Trust_then_IsTrusted_reports_true_only_for_that_fingerprint()
    {
        var (svc, _, _) = Build();
        var r = await svc.RecordLoginAsync(Tenant, UserId, new RequestInfo("1.1.1.1", "UA"));
        await svc.SetTrustedAsync(Tenant, UserId, r.Device.Id, true, AuditContext.System);

        Assert.True(await svc.IsTrustedAsync(Tenant, UserId, DeviceFingerprint.From("UA")));
        Assert.False(await svc.IsTrustedAsync(Tenant, UserId, DeviceFingerprint.From("OTHER")));
    }

    [Fact]
    public async Task Forget_removes_the_device()
    {
        var (svc, repo, _) = Build();
        var r = await svc.RecordLoginAsync(Tenant, UserId, new RequestInfo(null, "UA"));
        await svc.ForgetAsync(Tenant, UserId, r.Device.Id, AuditContext.System);
        Assert.Empty(repo.Items);
    }

    [Fact]
    public async Task Mutating_another_users_device_is_rejected()
    {
        var (svc, _, _) = Build();
        var r = await svc.RecordLoginAsync(Tenant, UserId, new RequestInfo(null, "UA"));
        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => svc.SetTrustedAsync(Tenant, Guid.NewGuid(), r.Device.Id, true, AuditContext.System));
    }

    private static (DeviceService, InMemoryDeviceRepo, ImpersonationRecordingAudit) Build()
    {
        var repo = new InMemoryDeviceRepo();
        var audit = new ImpersonationRecordingAudit();
        return (new DeviceService(repo, audit), repo, audit);
    }
}

internal sealed class InMemoryDeviceRepo : IUserDeviceRepository
{
    public readonly List<UserDevice> Items = new();
    public Task<IReadOnlyList<UserDevice>> ListForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<UserDevice>>(Items.Where(d => d.TenantId == tenantId && d.UserId == userId).ToList());
    public Task<UserDevice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(d => d.TenantId == tenantId && d.Id == id));
    public Task<UserDevice?> GetByFingerprintAsync(Guid tenantId, Guid userId, string fingerprint, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(d => d.TenantId == tenantId && d.UserId == userId && d.Fingerprint == fingerprint));
    public Task AddAsync(UserDevice device, CancellationToken ct = default)
    {
        if (device.Id == Guid.Empty) device.Id = Guid.NewGuid();
        Items.Add(device);
        return Task.CompletedTask;
    }
    public Task UpdateAsync(UserDevice device, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(UserDevice device, CancellationToken ct = default) { Items.Remove(device); return Task.CompletedTask; }
}
