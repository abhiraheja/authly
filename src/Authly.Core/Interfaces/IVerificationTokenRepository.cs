using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="VerificationToken"/>. Implemented in Infrastructure.</summary>
public interface IVerificationTokenRepository
{
    Task<VerificationToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(VerificationToken token, CancellationToken ct = default);
    Task UpdateAsync(VerificationToken token, CancellationToken ct = default);

    /// <summary>Invalidates any unused tokens of a given type for a user (single outstanding token per purpose).</summary>
    Task InvalidateOutstandingAsync(Guid userId, string type, CancellationToken ct = default);
}
