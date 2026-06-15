using Authly.Core.Enums;
using Authly.Modules.Common;

namespace Authly.Modules.AdvancedAuth;

/// <summary>
/// Tiered account recovery (Phase 11). The tiers, in order of preference, are: backup codes →
/// recovery contact → admin-assisted → identity verification. This service covers recovery-contact
/// management and the recovery-contact tier: initiating recovery notifies EVERY recovery channel
/// (the account email plus the user's recovery contacts) with a recovery link, and is fully audited.
/// Backup codes are handled by the MFA module; admin-assisted / identity-verification tiers are
/// follow-on work.
/// </summary>
public interface IRecoveryService
{
    Task<IReadOnlyList<RecoveryContactSummary>> ListContactsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task AddContactAsync(Guid tenantId, Guid userId, ContactType type, string value, AuditContext actor, CancellationToken ct = default);
    Task RemoveContactAsync(Guid tenantId, Guid userId, Guid contactId, AuditContext actor, CancellationToken ct = default);

    /// <summary>
    /// Begins recovery for the given email. Silent whether or not the account exists
    /// (anti-enumeration). On a hit, issues a recovery link and notifies all recovery channels.
    /// </summary>
    Task InitiateRecoveryAsync(Guid tenantId, string email, RequestInfo info, CancellationToken ct = default);
}
