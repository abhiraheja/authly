using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for one-time recovery codes. Looked up by user. Implemented in Infrastructure.</summary>
public interface IMfaBackupCodeRepository
{
    Task AddRangeAsync(IEnumerable<MfaBackupCode> codes, CancellationToken ct = default);

    /// <summary>Find an unused code by its hash for a specific user (the redemption lookup).</summary>
    Task<MfaBackupCode?> GetUnusedByHashAsync(Guid userId, string codeHash, CancellationToken ct = default);

    Task<int> CountUnusedAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Drops all of a user's existing codes (called before regenerating a fresh set).</summary>
    Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default);

    Task UpdateAsync(MfaBackupCode code, CancellationToken ct = default);
}
