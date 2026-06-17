using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="ConsentRecord"/>. Tenant-scoped. Implemented in Infrastructure.</summary>
public interface IConsentRecordRepository
{
    Task<IReadOnlyList<ConsentRecord>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AddAsync(ConsentRecord record, CancellationToken ct = default);
}
