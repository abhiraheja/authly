using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Append-only persistence for <see cref="LoginHistory"/>. Implemented in Infrastructure.</summary>
public interface ILoginHistoryRepository
{
    Task AddAsync(LoginHistory entry, CancellationToken ct = default);
    Task<IReadOnlyList<LoginHistory>> ListForUserAsync(Guid tenantId, Guid userId, int limit = 50, CancellationToken ct = default);
}
