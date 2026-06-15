using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for end-user <see cref="Session"/>s. Implemented in Infrastructure.</summary>
public interface ISessionRepository
{
    Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default);
    Task<IReadOnlyList<Session>> ListActiveForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AddAsync(Session session, CancellationToken ct = default);
    Task UpdateAsync(Session session, CancellationToken ct = default);

    /// <summary>Revokes all of a user's active sessions; returns the number revoked.</summary>
    Task<int> RevokeAllForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
