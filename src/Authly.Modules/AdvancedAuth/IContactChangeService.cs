using Authly.Core.Enums;
using Authly.Modules.Common;

namespace Authly.Modules.AdvancedAuth;

/// <summary>
/// Secure, cancellable change of a signed-in user's email or phone (Phase 11). The caller has
/// already proven the current identity (authenticated session). The new contact must be verified
/// before it's applied, and the OLD contact (the account email) is alerted with a cancel link.
/// </summary>
public interface IContactChangeService
{
    /// <summary>
    /// Starts a contact change: persists a pending change, emails a confirmation link to the new
    /// contact, and alerts the current email with a cancel link.
    /// </summary>
    Task<ContactChangeOutcome> RequestChangeAsync(Guid tenantId, Guid userId, ContactType type, string newValue,
        RequestInfo info, CancellationToken ct = default);

    /// <summary>Confirms the new contact (consumes the verify token) and applies it to the user. Returns success.</summary>
    Task<bool> VerifyAsync(Guid tenantId, string rawVerifyToken, CancellationToken ct = default);

    /// <summary>Cancels a pending change from the old contact's alert link. Returns true if a pending change was cancelled.</summary>
    Task<bool> CancelAsync(string rawCancelToken, CancellationToken ct = default);
}
