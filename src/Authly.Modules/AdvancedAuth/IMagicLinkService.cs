using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.AdvancedAuth;

/// <summary>
/// Passwordless "magic link" sign-in (Phase 11). A single-use, short-lived link is emailed; opening
/// it signs the user in. Tenant-scoped.
/// </summary>
public interface IMagicLinkService
{
    /// <summary>
    /// Issues a magic-link email if the address belongs to an active user. Silent either way
    /// (anti-enumeration) — the caller shows the same confirmation regardless.
    /// <paramref name="returnUrl"/> (a validated local URL, e.g. the /connect/authorize continuation)
    /// is carried in the link so the user lands back on the relying app after sign-in.
    /// </summary>
    Task RequestAsync(Guid tenantId, string email, RequestInfo info, string? returnUrl = null, CancellationToken ct = default);

    /// <summary>
    /// Consumes a magic-link token. Returns the user on success (single-use, unexpired, active),
    /// null otherwise. The caller starts the session + issues the cookie.
    /// </summary>
    Task<User?> CompleteAsync(Guid tenantId, string rawToken, CancellationToken ct = default);
}
