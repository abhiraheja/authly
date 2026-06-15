using System.Text.Json;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Common;

namespace Authly.Modules.Audit;

/// <inheritdoc />
public sealed class AuditLogger : IAuditLogger
{
    private readonly IAuditLogRepository _repo;

    public AuditLogger(IAuditLogRepository repo) => _repo = repo;

    public Task LogAsync(
        string @event,
        AuditContext actor,
        Guid? tenantId = null,
        string? resourceType = null,
        Guid? resourceId = null,
        string result = "success",
        object? metadata = null,
        CancellationToken ct = default)
    {
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
            CreatedAt = DateTimeOffset.UtcNow
        };

        return _repo.AddAsync(entry, ct);
    }
}
