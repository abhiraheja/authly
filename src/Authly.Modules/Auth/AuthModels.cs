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
/// </summary>
public interface IAuthUrlBuilder
{
    string BuildEmailVerificationUrl(Guid tenantId, string rawToken);
    string BuildPasswordResetUrl(Guid tenantId, string rawToken);
}
