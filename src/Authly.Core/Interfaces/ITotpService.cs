namespace Authly.Core.Interfaces;

/// <summary>
/// RFC 6238 Time-based One-Time Password primitives (HMAC-SHA1, 6 digits, 30s step — the
/// settings every standard authenticator app assumes). Implemented in Infrastructure.
/// </summary>
public interface ITotpService
{
    /// <summary>Generates a new random shared secret, Base32-encoded (the form apps expect).</summary>
    string GenerateSecret();

    /// <summary>
    /// Builds the <c>otpauth://totp/...</c> provisioning URI encoded into the enrollment QR code.
    /// </summary>
    string BuildProvisioningUri(string base32Secret, string accountName, string issuer);

    /// <summary>
    /// Verifies a user-entered code against the secret, tolerating +/- <paramref name="window"/>
    /// time steps for clock drift.
    /// </summary>
    bool Verify(string base32Secret, string code, int window = 1);
}
