using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Append-only persistence for <see cref="AuditLog"/>. Implemented in Infrastructure.</summary>
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog entry, CancellationToken ct = default);
}
