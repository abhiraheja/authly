using System.Text.Json;
using System.Text.Json.Nodes;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Events;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.Hooks;

/// <inheritdoc />
public sealed class PipelineHookService : IPipelineHookService
{
    private readonly IPipelineHookRepository _hooks;
    private readonly IPipelineHookClient _client;
    private readonly IEncryptionService _encryption;
    private readonly IAuditLogger _audit;
    private readonly ILogger<PipelineHookService> _logger;

    public PipelineHookService(
        IPipelineHookRepository hooks,
        IPipelineHookClient client,
        IEncryptionService encryption,
        IAuditLogger audit,
        ILogger<PipelineHookService> logger)
    {
        _hooks = hooks;
        _client = client;
        _encryption = encryption;
        _audit = audit;
        _logger = logger;
    }

    // --- Admin --------------------------------------------------------------

    public Task<IReadOnlyList<PipelineHook>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => _hooks.ListByTenantAsync(tenantId, ct);

    public Task<PipelineHook?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _hooks.GetByIdAsync(tenantId, id, ct);

    public async Task SaveAsync(Guid tenantId, PipelineHookInput input, AuditContext actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Url) || !Uri.TryCreate(input.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new PipelineHookConfigInvalidException("Enter a valid absolute http(s) URL.");

        var timeout = Math.Clamp(input.TimeoutMs, 250, 30_000);
        var existing = input.Id is { } id ? await _hooks.GetByIdAsync(tenantId, id, ct) : null;

        if (existing is null)
        {
            if (string.IsNullOrWhiteSpace(input.Secret))
                throw new PipelineHookConfigInvalidException("A signing secret is required.");
            await _hooks.AddAsync(new PipelineHook
            {
                TenantId = tenantId,
                Stage = input.Stage,
                Url = input.Url.Trim(),
                Secret = _encryption.Encrypt(input.Secret!),
                TimeoutMs = timeout,
                OnFailure = input.OnFailure,
                IsActive = input.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
        }
        else
        {
            existing.Stage = input.Stage;
            existing.Url = input.Url.Trim();
            existing.TimeoutMs = timeout;
            existing.OnFailure = input.OnFailure;
            existing.IsActive = input.IsActive;
            if (!string.IsNullOrWhiteSpace(input.Secret))
                existing.Secret = _encryption.Encrypt(input.Secret!);
            await _hooks.UpdateAsync(existing, ct);
        }

        await _audit.LogAsync("pipeline_hook.saved", actor, tenantId, "pipeline_hook", existing?.Id,
            metadata: new { stage = input.Stage.ToString(), input.Url, input.OnFailure }, ct: ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var hook = await _hooks.GetByIdAsync(tenantId, id, ct);
        if (hook is null) return;
        await _hooks.DeleteAsync(hook, ct);
        await _audit.LogAsync("pipeline_hook.deleted", actor, tenantId, "pipeline_hook", id, ct: ct);
    }

    // --- Execution ----------------------------------------------------------

    public async Task<PipelineStageResult> RunStageAsync(PipelineStage stage, Guid tenantId, object payload, CancellationToken ct = default)
    {
        var hooks = await _hooks.ListActiveByStageAsync(tenantId, stage, ct);
        if (hooks.Count == 0)
            return PipelineStageResult.Continue;

        var stageName = stage.ToString();
        var body = JsonSerializer.Serialize(payload);
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var hook in hooks)
        {
            var secret = DecryptOrNull(hook.Secret);
            if (secret is null)
            {
                _logger.LogWarning("Pipeline hook {HookId} secret unavailable; treating as failure.", hook.Id);
                if (hook.OnFailure == HookFailureMode.Block)
                    return PipelineStageResult.Block("hook_secret_unavailable");
                continue;
            }

            var result = await _client.InvokeAsync(
                new PipelineHookRequest(hook.Url, secret, stageName, body, hook.TimeoutMs), ct);

            if (!result.Success)
            {
                if (hook.OnFailure == HookFailureMode.Block)
                    return PipelineStageResult.Block(result.Error ?? "hook_failed");
                continue; // fail-open
            }

            MergeResponse(merged, result.ResponseJson);
        }

        return new PipelineStageResult { MergedData = merged };
    }

    private static void MergeResponse(Dictionary<string, string> merged, string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        JsonNode? node;
        try { node = JsonNode.Parse(json); }
        catch (JsonException) { return; }

        // A hook may either return claims directly or wrap them under a "claims" object.
        var source = node as JsonObject;
        if (source is null) return;
        if (source["claims"] is JsonObject wrapped) source = wrapped;

        foreach (var (key, value) in source)
        {
            if (value is null) continue;
            merged[key] = value is JsonValue v && v.TryGetValue<string>(out var s) ? s : value.ToJsonString();
        }
    }

    private string? DecryptOrNull(string cipher)
    {
        try { return _encryption.Decrypt(cipher); }
        catch (Exception) { return null; }
    }
}
