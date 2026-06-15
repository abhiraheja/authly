using Authly.Core.Deployment;
using Authly.Infrastructure.Deployment;
using Authly.Modules.Common;
using Authly.Modules.Compliance;

namespace Authly.Tests.Compliance;

public class SelfHostSyncAndDeploymentTests
{
    // --- SelfHostSyncService (cloud ingest) ---------------------------------

    [Fact]
    public async Task Register_issues_a_raw_key_and_stores_only_its_hash()
    {
        var repo = new FakeInstanceRepo();
        var hasher = new FakeTokenHasher();
        var svc = new SelfHostSyncService(repo, hasher, new RecordingAudit());

        var reg = await svc.RegisterAsync(ownerTenantId: null, name: "acme", AuditContext.System);

        var stored = repo.Items.Single();
        Assert.NotEqual(reg.RawSyncKey, stored.SyncKeyHash);          // raw never persisted
        Assert.Equal(hasher.Hash(reg.RawSyncKey), stored.SyncKeyHash); // stored as hash
        Assert.Equal("acme", stored.Name);
    }

    [Fact]
    public async Task Ingest_with_a_valid_key_records_clamped_aggregate_metrics()
    {
        var repo = new FakeInstanceRepo();
        var hasher = new FakeTokenHasher();
        var svc = new SelfHostSyncService(repo, hasher, new RecordingAudit());
        var reg = await svc.RegisterAsync(null, "acme", AuditContext.System);

        var ok = await svc.IngestAsync(reg.RawSyncKey,
            new SyncPayload("1.2.3", TenantCount: 2, UserCount: 40, AppCount: 5, ActiveSessionCount: -7, Status: "ok"));

        Assert.True(ok);
        var inst = repo.Items.Single();
        Assert.Equal("1.2.3", inst.Version);
        Assert.Equal(2, inst.TenantCount);
        Assert.Equal(40, inst.UserCount);
        Assert.Equal(5, inst.AppCount);
        Assert.NotNull(inst.LastSeenAt);
        Assert.Contains("\"active_sessions\":0", inst.Health); // negative clamped to 0
        Assert.Contains("\"status\":\"ok\"", inst.Health);
    }

    [Fact]
    public async Task Ingest_with_an_unknown_key_is_rejected()
    {
        var svc = new SelfHostSyncService(new FakeInstanceRepo(), new FakeTokenHasher(), new RecordingAudit());
        var rejected = await svc.IngestAsync("nope",
            new SyncPayload("1", 1, 1, 1, 1, "ok"));
        Assert.False(rejected);
    }

    // --- DeploymentContext (config parsing) ---------------------------------

    [Fact]
    public void Cloud_is_the_default_and_sync_is_disabled()
    {
        var ctx = new DeploymentContext(new FakeConfig(new() { ["APP_VERSION"] = "9.9.9" }));
        Assert.Equal(DeploymentMode.Cloud, ctx.Mode);
        Assert.False(ctx.SyncEnabled);
        Assert.Equal("9.9.9", ctx.Version);
    }

    [Fact]
    public void Self_hosted_with_endpoint_and_key_enables_sync()
    {
        var ctx = new DeploymentContext(new FakeConfig(new()
        {
            ["DEPLOYMENT_MODE"] = "self_hosted",
            ["SYNC_ENDPOINT"] = "https://cloud/api/sync",
            ["SYNC_KEY"] = "k"
        }));
        Assert.Equal(DeploymentMode.SelfHosted, ctx.Mode);
        Assert.True(ctx.SyncEnabled);
    }

    [Fact]
    public void Self_hosted_without_a_key_does_not_enable_sync()
    {
        var ctx = new DeploymentContext(new FakeConfig(new()
        {
            ["DEPLOYMENT_MODE"] = "self_hosted",
            ["SYNC_ENDPOINT"] = "https://cloud/api/sync"
        }));
        Assert.Equal(DeploymentMode.SelfHosted, ctx.Mode);
        Assert.False(ctx.SyncEnabled);
    }
}
