namespace Authly.Core.WebAuthn;

/// <summary>The user a passkey is being registered for (WebAuthn user handle + names).</summary>
public sealed record WebAuthnUser(byte[] Id, string Name, string DisplayName);

/// <summary>
/// A WebAuthn ceremony challenge: <see cref="OptionsJson"/> is handed to the browser's
/// <c>navigator.credentials</c> call; <see cref="State"/> is an opaque blob the web layer stashes
/// (data-protected cookie) and returns on completion so the gateway can match the challenge.
/// </summary>
public sealed record WebAuthnChallenge(string OptionsJson, string State);

/// <summary>A newly-registered credential to persist (raw bytes; the module base64url-encodes for storage).</summary>
public sealed record WebAuthnNewCredential(byte[] CredentialId, byte[] PublicKey, uint SignCount, Guid Aaguid);

/// <summary>A stored passkey supplied to the gateway when verifying an assertion.</summary>
public sealed record WebAuthnStoredCredential(byte[] CredentialId, byte[] PublicKey, uint SignCount, byte[] UserHandle);

/// <summary>The result of verifying an assertion: which credential signed, and its new counter.</summary>
public sealed record WebAuthnAssertionResult(byte[] CredentialId, uint NewSignCount);

/// <summary>The WebAuthn relying-party identity for the current request (rpId + full origin).</summary>
public sealed record RelyingPartyInfo(string RpId, string Origin);

/// <summary>
/// Supplies the relying-party id/origin for the current request. Implemented in the web layer
/// (from the request host) so the Infrastructure gateway stays free of HTTP-context types — the
/// rpId must match the host the user is on (localhost or a tenant's custom domain).
/// </summary>
public interface IWebAuthnRelyingParty
{
    RelyingPartyInfo Current();
}

/// <summary>Thrown when a WebAuthn ceremony fails verification (bad attestation/assertion, replay, etc.).</summary>
public sealed class WebAuthnException : Exception
{
    public WebAuthnException(string message, Exception? inner = null) : base(message, inner) { }
}

/// <summary>
/// Gateway over the WebAuthn/FIDO2 ceremony crypto (implemented in Infrastructure with a FIDO2
/// library). Core/Modules depend only on this seam — no library types leak past Infrastructure,
/// mirroring <c>ISocialAuthGateway</c>.
/// </summary>
public interface IWebAuthnGateway
{
    /// <summary>Builds registration options for a user, excluding their already-registered credentials.</summary>
    WebAuthnChallenge BeginRegistration(WebAuthnUser user, IReadOnlyList<byte[]> excludeCredentialIds);

    /// <summary>Verifies the browser's attestation response against the challenge state; returns the credential to store.</summary>
    Task<WebAuthnNewCredential> CompleteRegistrationAsync(string state, string responseJson, CancellationToken ct = default);

    /// <summary>Builds assertion (login) options allowing the given credential ids.</summary>
    WebAuthnChallenge BeginAssertion(IReadOnlyList<byte[]> allowedCredentialIds);

    /// <summary>Verifies the browser's assertion response against the challenge state and the stored credentials.</summary>
    Task<WebAuthnAssertionResult> CompleteAssertionAsync(string state, string responseJson,
        IReadOnlyList<WebAuthnStoredCredential> credentials, CancellationToken ct = default);
}
