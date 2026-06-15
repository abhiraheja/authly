using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Events;
using Authly.Core.Interfaces;
using Authly.Infrastructure.Security;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Hooks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authly.Tests.Webhooks;

public class PipelineHookServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task No_hooks_continues_with_no_merge()
    {
        var h = new Harness();
        var result = await h.Service.RunStageAsync(PipelineStage.PreToken, Tenant, new { });

        Assert.False(result.Blocked);
        Assert.Empty(result.MergedData);
    }

    [Fact]
    public async Task Block_mode_failure_blocks_the_flow()
    {
        var h = new Harness();
        h.AddHook(PipelineStage.PreRegistration, HookFailureMode.Block);
        h.Client.Result = PipelineHookResult.Fail(500, "HTTP 500");

        var result = await h.Service.RunStageAsync(PipelineStage.PreRegistration, Tenant, new { });

        Assert.True(result.Blocked);
    }

    [Fact]
    public async Task Continue_mode_failure_does_not_block()
    {
        var h = new Harness();
        h.AddHook(PipelineStage.PreRegistration, HookFailureMode.Continue);
        h.Client.Result = PipelineHookResult.Fail(null, "timeout");

        var result = await h.Service.RunStageAsync(PipelineStage.PreRegistration, Tenant, new { });

        Assert.False(result.Blocked);
        Assert.Empty(result.MergedData);
    }

    [Fact]
    public async Task Successful_hook_merges_claims_from_response()
    {
        var h = new Harness();
        h.AddHook(PipelineStage.PreToken, HookFailureMode.Continue);
        h.Client.Result = PipelineHookResult.Ok(200, """{"tier":"gold","region":"eu"}""");

        var result = await h.Service.RunStageAsync(PipelineStage.PreToken, Tenant, new { });

        Assert.False(result.Blocked);
        Assert.Equal("gold", result.MergedData["tier"]);
        Assert.Equal("eu", result.MergedData["region"]);
    }

    [Fact]
    public async Task Successful_hook_unwraps_a_claims_envelope()
    {
        var h = new Harness();
        h.AddHook(PipelineStage.PreToken, HookFailureMode.Continue);
        h.Client.Result = PipelineHookResult.Ok(200, """{"claims":{"plan":"pro"}}""");

        var result = await h.Service.RunStageAsync(PipelineStage.PreToken, Tenant, new { });

        Assert.Equal("pro", result.MergedData["plan"]);
    }

    private sealed class Harness
    {
        public readonly FakeHookRepo Hooks = new();
        public readonly FakeClient Client = new();
        public readonly AesEncryptionService Encryption =
            new(Options.Create(new EncryptionOptions { Key = "3J8mZ1qg9X0vQpYb2sR7tU4wK6nL5cD8eF1aH0iJ2kM=" }));
        public readonly PipelineHookService Service;

        public Harness() => Service = new PipelineHookService(Hooks, Client, Encryption, new NullAudit(),
            NullLogger<PipelineHookService>.Instance);

        public void AddHook(PipelineStage stage, HookFailureMode onFailure) => Hooks.Items.Add(new PipelineHook
        {
            Id = Guid.NewGuid(), TenantId = Tenant, Stage = stage, Url = "https://hook",
            Secret = Encryption.Encrypt("secret"), TimeoutMs = 3000, OnFailure = onFailure, IsActive = true
        });
    }

    private sealed class FakeHookRepo : IPipelineHookRepository
    {
        public readonly List<PipelineHook> Items = new();
        public Task<IReadOnlyList<PipelineHook>> ListByTenantAsync(Guid t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PipelineHook>>(Items.Where(x => x.TenantId == t).ToList());
        public Task<IReadOnlyList<PipelineHook>> ListActiveByStageAsync(Guid t, PipelineStage s, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PipelineHook>>(Items.Where(x => x.TenantId == t && x.Stage == s && x.IsActive).ToList());
        public Task<PipelineHook?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task AddAsync(PipelineHook x, CancellationToken ct = default) { Items.Add(x); return Task.CompletedTask; }
        public Task UpdateAsync(PipelineHook x, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(PipelineHook x, CancellationToken ct = default) { Items.Remove(x); return Task.CompletedTask; }
    }

    private sealed class FakeClient : IPipelineHookClient
    {
        public PipelineHookResult Result = PipelineHookResult.Ok(200, null);
        public Task<PipelineHookResult> InvokeAsync(PipelineHookRequest request, CancellationToken ct = default)
            => Task.FromResult(Result);
    }

    private sealed class NullAudit : IAuditLogger
    {
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
            Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
