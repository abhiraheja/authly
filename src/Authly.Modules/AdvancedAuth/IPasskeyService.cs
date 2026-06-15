using Authly.Core.Entities;
using Authly.Core.WebAuthn;
using Authly.Modules.Common;

namespace Authly.Modules.AdvancedAuth;

/// <summary>
/// Passkey (WebAuthn/FIDO2) enrolment and passwordless login (Phase 11). Credentials are stored as
/// MFA factors of type passkey. The ceremony challenge <c>State</c> is carried by the web layer
/// (data-protected cookie) between begin/complete. Tenant-scoped.
/// </summary>
public interface IPasskeyService
{
    /// <summary>Begins registration for a signed-in user; returns options for the browser + the challenge state.</summary>
    Task<WebAuthnChallenge> BeginRegistrationAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>Verifies the attestation and stores the new passkey. Throws <see cref="WebAuthnException"/> on failure.</summary>
    Task CompleteRegistrationAsync(Guid tenantId, Guid userId, string state, string responseJson, string? friendlyName,
        AuditContext actor, CancellationToken ct = default);

    /// <summary>Begins a passwordless login assertion for the user's enrolled passkeys (null if they have none).</summary>
    Task<WebAuthnChallenge?> BeginLoginAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Verifies a login assertion, updates the signature counter, and returns the user on success
    /// (null on failure). The caller starts the session + issues the cookie.
    /// </summary>
    Task<User?> CompleteLoginAsync(Guid tenantId, Guid userId, string state, string responseJson, CancellationToken ct = default);

    Task<IReadOnlyList<PasskeySummary>> ListAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task RemoveAsync(Guid tenantId, Guid userId, Guid factorId, AuditContext actor, CancellationToken ct = default);
}
