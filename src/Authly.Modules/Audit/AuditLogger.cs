using System.Text.Json;
using Authly.Core.Entities;
using Authly.Core.Events;
using Authly.Core.Interfaces;
using Authly.Modules.Common;

namespace Authly.Modules.Audit;

/// <inheritdoc />
public sealed class AuditLogger : IAuditLogger
{
    private readonly IAuditLogRepository _repo;
    private readonly IEventPublisher _events;

    public AuditLogger(IAuditLogRepository repo, IEventPublisher events)
    {
        _repo = repo;
        _events = events;
    }

    public async Task LogAsync(
        string @event,
        AuditContext actor,
        Guid? tenantId = null,
        string? resourceType = null,
        Guid? resourceId = null,
        string result = "success",
        object? metadata = null,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = new AuditLog
        {
            TenantId = tenantId,
            ActorId = actor.ActorId,
            ActorType = actor.ActorType,
            Event = @event,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Result = result,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            Metadata = metadata is null ? "{}" : JsonSerializer.Serialize(metadata),
            CreatedAt = now
        };

        await _repo.AddAsync(entry, ct);

        // Fan the audited event out to subscribed webhook endpoints (§4.12). Tenant-scoped only;
        // platform/super-admin events (no tenant) are not delivered. Best-effort — never throws.
        if (tenantId is { } tid)
        {
            await _events.PublishAsync(new EventEnvelope(
                @event, tid, now, actor.ActorId, actor.ActorType, resourceType, resourceId, result, metadata), ct);
        }
    }
}
