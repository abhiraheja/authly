using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="AccountInviteToken"/> (global / RLS-exempt). Implemented in Infrastructure.</summary>
public interface IAccountInviteTokenRepository
{
    Task<AccountInviteToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default);
    Task AddAsync(AccountInviteToken token, CancellationToken ct = default);
    Task UpdateAsync(AccountInviteToken token, CancellationToken ct = default);

    /// <summary>Invalidates any unused invite tokens for an account+org before issuing a new one.</summary>
    Task InvalidateOutstandingAsync(Guid accountId, Guid organizationId, CancellationToken ct = default);
}
