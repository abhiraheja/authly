using System.Text.Json;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Hooks;

namespace Authly.Modules.Claims;

/// <inheritdoc />
public sealed class TokenClaimAssembler : ITokenClaimAssembler
{
    private readonly IClaimConfigRepository _configs;
    private readonly IPipelineHookService _hooks;

    public TokenClaimAssembler(IClaimConfigRepository configs, IPipelineHookService hooks)
    {
        _configs = configs;
        _hooks = hooks;
    }

    public async Task<ClaimAssemblyResult> AssembleAsync(ClaimAssemblyRequest request, CancellationToken ct = default)
    {
        var claims = new Dictionary<string, string>(StringComparer.Ordinal);

        var configs = (await _configs.ListForIssuanceAsync(request.TenantId, request.ApplicationId, ct))
            .Where(c => c.TokenType == request.TokenType);

        using var userMeta = Parse(request.UserMetadataJson);
        using var appMeta = Parse(request.AppMetadataJson);

        // Step 2 — static custom claims; Step 3 — metadata-mapped claims.
        foreach (var config in configs)
        {
            switch (config.Type)
            {
                case ClaimSourceType.Static when config.Source is { } value:
                    claims[config.ClaimName] = value;
                    break;
                case ClaimSourceType.Metadata when config.Source is { } path:
                    if (ResolveMetadata(path, userMeta?.RootElement, appMeta?.RootElement) is { } resolved)
                        claims[config.ClaimName] = resolved;
                    break;
            }
        }

        // Step 4 — webhook claims from pre-token pipeline hooks; respect timeout/on_failure.
        if (!request.RunPreTokenHooks)
            return new ClaimAssemblyResult(claims);

        var stage = await _hooks.RunStageAsync(PipelineStage.PreToken, request.TenantId, request.HookPayload, ct);
        if (stage.Blocked)
            return new ClaimAssemblyResult(claims, Blocked: true, BlockReason: stage.BlockReason);

        foreach (var (key, value) in stage.MergedData)
            claims[key] = value; // hook output wins for any overlapping custom claim

        return new ClaimAssemblyResult(claims);
    }

    private static JsonDocument? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonDocument.Parse(json); }
        catch (JsonException) { return null; }
    }

    /// <summary>
    /// Resolves a dotted path against user/app metadata. A path may be prefixed with
    /// <c>user_metadata.</c> or <c>app_metadata.</c> to select the source explicitly; otherwise
    /// user metadata is tried first, then app metadata.
    /// </summary>
    private static string? ResolveMetadata(string path, JsonElement? user, JsonElement? app)
    {
        if (path.StartsWith("user_metadata.", StringComparison.Ordinal))
            return Navigate(user, path["user_metadata.".Length..]);
        if (path.StartsWith("app_metadata.", StringComparison.Ordinal))
            return Navigate(app, path["app_metadata.".Length..]);
        return Navigate(user, path) ?? Navigate(app, path);
    }

    private static string? Navigate(JsonElement? root, string dottedPath)
    {
        if (root is not { } element || element.ValueKind != JsonValueKind.Object)
            return null;

        var current = element;
        foreach (var segment in dottedPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return null;
            current = next;
        }

        return current.ValueKind switch
        {
            JsonValueKind.String => current.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => current.GetRawText(),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => current.GetRawText() // object/array → raw JSON string
        };
    }
}
