using System.Text.Json;
using System.Text.Json.Nodes;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Compliance;

/// <summary>
/// Cloud control-plane side of self-host telemetry. Issues one-time sync keys (stored hashed) and
/// records aggregate pushes. Platform-level — not tenant-scoped.
/// </summary>
public sealed class SelfHostSyncService : ISelfHostSyncService
{
    private readonly ISelfHostedInstanceRepository _instances;
    private readonly ITokenHasher _tokens;
    private readonly IAuditLogger _audit;

    public SelfHostSyncService(ISelfHostedInstanceRepository instances, ITokenHasher tokens, IAuditLogger audit)
    {
        _instances = instances;
        _tokens = tokens;
        _audit = audit;
    }

    public async Task<InstanceRegistration> RegisterAsync(Guid? ownerTenantId, string? name, AuditContext actor, CancellationToken ct = default)
    {
        var rawKey = _tokens.GenerateRawToken();
        var instance = new SelfHostedInstance
        {
            OwnerTenantId = ownerTenantId,
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            SyncKeyHash = _tokens.Hash(rawKey),
            Health = "{}",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _instances.AddAsync(instance, ct);

        // Audit records the act, never the raw key.
        await _audit.LogAsync("self_host_instance.registered", actor, tenantId: ownerTenantId,
            resourceType: "self_hosted_instance", resourceId: instance.Id, ct: ct);

        return new InstanceRegistration(instance.Id, rawKey);
    }

    public Task<IReadOnlyList<SelfHostedInstance>> ListAsync(CancellationToken ct = default)
        => _instances.ListAsync(ct);

    public async Task<bool> IngestAsync(string rawSyncKey, SyncPayload payload, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawSyncKey)) return false;

        var instance = await _instances.GetBySyncKeyHashAsync(_tokens.Hash(rawSyncKey), ct);
        if (instance is null) return false;

        // Defensive clamp: store non-negative counts only; ignore anything else in the payload.
        instance.Version = string.IsNullOrWhiteSpace(payload.Version) ? null : payload.Version.Trim();
        instance.TenantCount = Math.Max(0, payload.TenantCount);
        instance.UserCount = Math.Max(0, payload.UserCount);
        instance.AppCount = Math.Max(0, payload.AppCount);
        instance.LastSeenAt = DateTimeOffset.UtcNow;
        instance.Health = new JsonObject
        {
            ["status"] = string.IsNullOrWhiteSpace(payload.Status) ? "unknown" : payload.Status.Trim(),
            ["active_sessions"] = Math.Max(0, payload.ActiveSessionCount)
        }.ToJsonString(new JsonSerializerOptions());

        await _instances.UpdateAsync(instance, ct);
        return true;
    }
}
