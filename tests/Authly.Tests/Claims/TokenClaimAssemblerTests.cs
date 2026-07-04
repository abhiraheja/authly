using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Modules.Claims;
using Authly.Modules.Hooks;

namespace Authly.Tests.Claims;

public class TokenClaimAssemblerTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Static_and_metadata_claims_are_assembled_in_order()
    {
        var h = new Harness();
        h.Configs.Items.Add(Config(ClaimSourceType.Static, "plan", "enterprise"));
        h.Configs.Items.Add(Config(ClaimSourceType.Metadata, "department", "department"));        // user_metadata
        h.Configs.Items.Add(Config(ClaimSourceType.Metadata, "tier", "app_metadata.billing.tier")); // app_metadata path
        h.Configs.Items.Add(Config(ClaimSourceType.Metadata, "missing", "nope.absent"));            // unresolved → skipped

        var result = await h.Service.AssembleAsync(new ClaimAssemblyRequest(
            Tenant, null, ClaimTokenType.Access,
            UserMetadataJson: """{"department":"engineering"}""",
            AppMetadataJson: """{"billing":{"tier":"gold"}}""",
            HookPayload: new { }));

        Assert.False(result.Blocked);
        Assert.Equal("enterprise", result.Claims["plan"]);
        Assert.Equal("engineering", result.Claims["department"]);
        Assert.Equal("gold", result.Claims["tier"]);
        Assert.False(result.Claims.ContainsKey("missing"));
    }

    [Fact]
    public async Task Pre_token_hook_claims_are_merged_when_hooks_run()
    {
        var h = new Harness();
        h.Hooks.Result = new PipelineStageResult
        {
            MergedData = new Dictionary<string, string> { ["risk"] = "low" }
        };

        var result = await h.Service.AssembleAsync(new ClaimAssemblyRequest(
            Tenant, null, ClaimTokenType.Access, null, null, new { }, RunPreTokenHooks: true));

        Assert.Equal("low", result.Claims["risk"]);
    }

    [Fact]
    public async Task Hook_produced_claims_are_reported_in_HookClaimNames()
    {
        // The issuer uses HookClaimNames to decide a hook may override reserved authorization claims
        // (role/permissions) that a static/metadata claim cannot. Static claims stay off that list.
        var h = new Harness();
        h.Configs.Items.Add(Config(ClaimSourceType.Static, "plan", "enterprise"));
        h.Hooks.Result = new PipelineStageResult
        {
            MergedData = new Dictionary<string, string> { ["permissions"] = "READ WRITE", ["role"] = "admin" }
        };

        var result = await h.Service.AssembleAsync(new ClaimAssemblyRequest(
            Tenant, null, ClaimTokenType.Access, null, null, new { }, RunPreTokenHooks: true));

        Assert.NotNull(result.HookClaimNames);
        Assert.Contains("permissions", result.HookClaimNames!);
        Assert.Contains("role", result.HookClaimNames!);
        Assert.DoesNotContain("plan", result.HookClaimNames!); // static config, not hook-sourced
    }

    [Fact]
    public async Task Pre_token_hooks_are_skipped_when_not_requested()
    {
        var h = new Harness();
        h.Hooks.Result = new PipelineStageResult { MergedData = new Dictionary<string, string> { ["risk"] = "low" } };

        var result = await h.Service.AssembleAsync(new ClaimAssemblyRequest(
            Tenant, null, ClaimTokenType.Id, null, null, new { }, RunPreTokenHooks: false));

        Assert.False(h.Hooks.WasCalled);
        Assert.False(result.Claims.ContainsKey("risk"));
    }

    [Fact]
    public async Task Blocking_pre_token_hook_blocks_issuance()
    {
        var h = new Harness();
        h.Hooks.Result = PipelineStageResult.Block("fraud");

        var result = await h.Service.AssembleAsync(new ClaimAssemblyRequest(
            Tenant, null, ClaimTokenType.Access, null, null, new { }));

        Assert.True(result.Blocked);
        Assert.Equal("fraud", result.BlockReason);
    }

    [Fact]
    public async Task Only_configs_for_the_requested_token_type_apply()
    {
        var h = new Harness();
        h.Configs.Items.Add(Config(ClaimSourceType.Static, "a", "1", ClaimTokenType.Access));
        h.Configs.Items.Add(Config(ClaimSourceType.Static, "i", "2", ClaimTokenType.Id));

        var access = await h.Service.AssembleAsync(new ClaimAssemblyRequest(
            Tenant, null, ClaimTokenType.Access, null, null, new { }, RunPreTokenHooks: false));

        Assert.True(access.Claims.ContainsKey("a"));
        Assert.False(access.Claims.ContainsKey("i"));
    }

    private static ClaimConfig Config(ClaimSourceType type, string name, string source, ClaimTokenType token = ClaimTokenType.Access) => new()
    {
        Id = Guid.NewGuid(), TenantId = Tenant, TokenType = token, Type = type, ClaimName = name, Source = source
    };

    private sealed class Harness
    {
        public readonly FakeConfigRepo Configs = new();
        public readonly FakeHooks Hooks = new();
        public readonly TokenClaimAssembler Service;

        public Harness() => Service = new TokenClaimAssembler(Configs, Hooks);
    }

    private sealed class FakeConfigRepo : Authly.Core.Interfaces.IClaimConfigRepository
    {
        public readonly List<ClaimConfig> Items = new();
        public Task<IReadOnlyList<ClaimConfig>> ListByTenantAsync(Guid t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClaimConfig>>(Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<ClaimConfig>> ListForIssuanceAsync(Guid t, Guid? app, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<ClaimConfig>>(Items
                .Where(x => x.TenantId == t && (x.ApplicationId == null || x.ApplicationId == app)).ToList());
        public Task<ClaimConfig?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task AddAsync(ClaimConfig c, CancellationToken ct = default) { Items.Add(c); return Task.CompletedTask; }
        public Task DeleteAsync(ClaimConfig c, CancellationToken ct = default) { Items.Remove(c); return Task.CompletedTask; }
    }

    private sealed class FakeHooks : IPipelineHookService
    {
        public PipelineStageResult Result = PipelineStageResult.Continue;
        public bool WasCalled;
        public Task<PipelineStageResult> RunStageAsync(PipelineStage stage, Guid tenantId, object payload, CancellationToken ct = default)
        {
            WasCalled = true;
            return Task.FromResult(Result);
        }
        public Task<IReadOnlyList<PipelineHook>> ListAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PipelineHook?> GetAsync(Guid t, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveAsync(Guid t, PipelineHookInput i, Authly.Modules.Common.AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteAsync(Guid t, Guid id, Authly.Modules.Common.AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
