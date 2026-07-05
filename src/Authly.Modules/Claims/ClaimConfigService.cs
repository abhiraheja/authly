using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Claims;

/// <inheritdoc />
public sealed class ClaimConfigService : IClaimConfigService
{
    // Reserved/standard claim names a tenant must not override (§5.6 step 1 owns these).
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "sub", "iss", "aud", "exp", "iat", "nbf", "jti", "email", "email_verified",
        "name", "tenant_id", "roles", "role", "permissions", "scope"
    };

    // Authorization claims a pre-token pipeline hook is allowed to own (mirrors the issuer's
    // HookOverridableClaims). A `Hook` config never sources a value — it only routes an
    // already-trusted hook claim into another token type (e.g. the id_token) — so declaring one
    // for these names is safe even though a Static/Metadata config must not override them.
    private static readonly HashSet<string> HookRoutable = new(StringComparer.OrdinalIgnoreCase)
    {
        "role", "permissions"
    };

    private readonly IClaimConfigRepository _repo;
    private readonly IAuditLogger _audit;

    public ClaimConfigService(IClaimConfigRepository repo, IAuditLogger audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public Task<IReadOnlyList<ClaimConfig>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => _repo.ListByTenantAsync(tenantId, ct);

    public async Task AddAsync(Guid tenantId, ClaimConfigInput input, AuditContext actor, CancellationToken ct = default)
    {
        var name = input.ClaimName?.Trim() ?? "";
        if (name.Length == 0)
            throw new ClaimConfigInvalidException("Enter a claim name.");
        if (Reserved.Contains(name) && !(input.Type == ClaimSourceType.Hook && HookRoutable.Contains(name)))
            throw new ClaimConfigInvalidException($"'{name}' is a reserved claim and cannot be overridden.");

        // Per-claim Webhook URLs aren't supported here — webhook claim VALUES come from the signed
        // pre-token pipeline hook (§5.6 step 4). A `Hook` config doesn't fetch a value; it only
        // declares that a pipeline-hook claim is also written to the chosen token (e.g. the id_token),
        // so it needs no Source.
        if (input.Type == ClaimSourceType.Webhook)
            throw new ClaimConfigInvalidException("Use a pre-token pipeline hook for webhook-sourced claims.");

        if (input.Type != ClaimSourceType.Hook && string.IsNullOrWhiteSpace(input.Source))
            throw new ClaimConfigInvalidException(input.Type == ClaimSourceType.Static
                ? "Enter the static value." : "Enter the metadata path.");

        await _repo.AddAsync(new ClaimConfig
        {
            TenantId = tenantId,
            ApplicationId = input.ApplicationId,
            TokenType = input.TokenType,
            Type = input.Type,
            ClaimName = name,
            Source = input.Type == ClaimSourceType.Hook ? null : input.Source!.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _audit.LogAsync("claim_config.created", actor, tenantId, "claim_config", null,
            metadata: new { claim = name, type = input.Type.ToString(), token = input.TokenType.ToString() }, ct: ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var config = await _repo.GetByIdAsync(tenantId, id, ct);
        if (config is null) return;
        await _repo.DeleteAsync(config, ct);
        await _audit.LogAsync("claim_config.deleted", actor, tenantId, "claim_config", id, ct: ct);
    }
}
