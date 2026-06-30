using Authly.Core.Entities;
using Authly.Core.Enums;

namespace Authly.Modules.Applications;

/// <summary>Input for registering a new OAuth client under a tenant.</summary>
public sealed record CreateApplicationRequest(
    string Name,
    ApplicationType Type,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool AllowSignup = true);

/// <summary>
/// Editable fields of an existing OAuth client. The type, client id, grant types and secret are
/// immutable here — only the display name, redirect URIs, post-logout redirect URIs and requested
/// scopes can change.
/// </summary>
public sealed record UpdateApplicationRequest(
    string Name,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> PostLogoutRedirectUris,
    bool AllowSignup = true);

/// <summary>
/// Result of creating/rotating credentials. <see cref="ClientSecret"/> is the raw secret and is
/// returned to the caller exactly once (shown to the tenant, then unrecoverable). Null for public
/// clients, which have no secret.
/// </summary>
public sealed record ApplicationSecretResult(Application Application, string? ClientSecret);

/// <summary>Thrown when an operation targets an application that doesn't exist in the tenant.</summary>
public sealed class ApplicationNotFoundException : Exception
{
    public ApplicationNotFoundException(Guid id) : base($"Application {id} was not found.") { }
}

/// <summary>Thrown when a secret operation is attempted on a public (non-confidential) client.</summary>
public sealed class PublicClientHasNoSecretException : Exception
{
    public PublicClientHasNoSecretException() : base("Public clients (SPA/native) use PKCE and have no client secret.") { }
}
