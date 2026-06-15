using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Auth;

/// <summary>
/// End-user authentication for a single tenant: registration, email/password login with
/// session + login-history recording, email verification, and password reset. All
/// operations are tenant-scoped — the caller passes the resolved tenant id.
/// </summary>
public interface IAuthService
{
    /// <summary>Creates a user (Argon2id hash), issues an email-verification token, and queues the email.</summary>
    Task<User> RegisterAsync(Guid tenantId, RegisterRequest request, RequestInfo info, CancellationToken ct = default);

    /// <summary>Validates credentials; on success creates a session and updates last-login. Always records login history.</summary>
    Task<LoginResult> AuthenticateAsync(Guid tenantId, string email, string password, RequestInfo info, CancellationToken ct = default);

    /// <summary>Re-issues a verification email if the user exists and is still unverified. Silent either way.</summary>
    Task ResendVerificationEmailAsync(Guid tenantId, string email, CancellationToken ct = default);

    /// <summary>
    /// Consumes a verification token (single-use, expiring) within the tenant's context and
    /// marks the user's email verified. Tenant-scoped because the user table enforces RLS;
    /// the link is clicked within the owning tenant (dev cookie / prod custom domain).
    /// </summary>
    Task<bool> VerifyEmailAsync(Guid tenantId, string rawToken, CancellationToken ct = default);

    /// <summary>Issues a reset token + email if the email exists. Returns no signal about existence (anti-enumeration).</summary>
    Task RequestPasswordResetAsync(Guid tenantId, string email, RequestInfo info, CancellationToken ct = default);

    /// <summary>Consumes a reset token (single-use, expiring) within the tenant's context, sets the new password, and revokes active sessions.</summary>
    Task<bool> ResetPasswordAsync(Guid tenantId, string rawToken, string newPassword, CancellationToken ct = default);

    /// <summary>Returns the session if it is active (exists, not revoked, not expired); null otherwise.</summary>
    Task<Session?> GetActiveSessionAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Revokes a session (logout).</summary>
    Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default);
}
