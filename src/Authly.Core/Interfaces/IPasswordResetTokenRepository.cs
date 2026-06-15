using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="PasswordResetToken"/>. Implemented in Infrastructure.</summary>
public interface IPasswordResetTokenRepository
{
    Task<PasswordResetToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(PasswordResetToken token, CancellationToken ct = default);
    Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default);

    /// <summary>Invalidates any unused reset tokens for a user before issuing a new one.</summary>
    Task InvalidateOutstandingAsync(Guid userId, CancellationToken ct = default);
}
