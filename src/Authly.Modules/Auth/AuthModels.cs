using Authly.Core.Entities;

namespace Authly.Modules.Auth;

/// <summary>Self-service registration input for a new end-user within a tenant.</summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    string? FirstName = null,
    string? LastName = null);

/// <summary>Outcome of an authentication attempt.</summary>
public enum LoginOutcome
{
    Success,
    InvalidCredentials,
    Suspended
}

/// <summary>
/// Result of <see cref="IAuthService.AuthenticateAsync"/>. On success carries the user and
/// the freshly created session whose id the caller places in the auth cookie.
/// </summary>
public sealed record LoginResult(LoginOutcome Outcome, User? User = null, Session? Session = null)
{
    public bool Succeeded => Outcome == LoginOutcome.Success;
}

/// <summary>Thrown when registration is attempted with an email already taken in the tenant.</summary>
public sealed class EmailAlreadyExistsException : Exception
{
    public EmailAlreadyExistsException(string email)
        : base($"An account with email '{email}' already exists in this tenant.") { }
}

/// <summary>
/// Builds the absolute URLs embedded in verification / reset emails. Implemented in the web
/// layer (which owns routing); the auth module depends only on this abstraction so it never
/// references HTTP/MVC types.
///
/// Tenant-scoped links carry the tenant slug as a <c>&amp;tenant=</c> query param so they resolve
/// on the shared platform host (e.g. <c>authly.saarvix.in</c>) regardless of which browser opens
/// them — the host alone can't identify the tenant, and the tenant-hint cookie only exists in the
/// browser that requested the link. Resolving the slug requires a repository lookup, hence the
/// methods are async.
/// </summary>
public interface IAuthUrlBuilder
{
    Task<string> BuildEmailVerificationUrl(Guid tenantId, string rawToken);
    Task<string> BuildPasswordResetUrl(Guid tenantId, string rawToken);

    // Phase 11 — advanced auth links.
    /// <summary>Builds the magic sign-in link. When <paramref name="returnUrl"/> is supplied (e.g. the
    /// local /connect/authorize continuation), it is carried through so the user lands back on the
    /// relying app after sign-in.</summary>
    Task<string> BuildMagicLinkUrl(Guid tenantId, string rawToken, string? returnUrl = null);
    Task<string> BuildContactChangeVerifyUrl(Guid tenantId, string rawToken);
    Task<string> BuildContactChangeCancelUrl(Guid tenantId, string rawToken);
    Task<string> BuildRecoveryUrl(Guid tenantId, string rawToken);

    /// <summary>Phase 4 — the public, tenant-less operator-invite accept link.</summary>
    string BuildInviteAcceptUrl(string rawToken);
}
