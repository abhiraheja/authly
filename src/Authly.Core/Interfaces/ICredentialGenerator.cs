namespace Authly.Core.Interfaces;

/// <summary>
/// Generates OAuth client credentials in the platform's canonical formats. Implemented with a
/// CSPRNG in Infrastructure. The raw secret is returned once to the caller and only ever stored
/// hashed.
/// </summary>
public interface ICredentialGenerator
{
    /// <summary><c>client_</c> + 24 random URL-safe characters.</summary>
    string GenerateClientId();

    /// <summary><c>secret_</c> + 48 random URL-safe characters.</summary>
    string GenerateClientSecret();

    /// <summary><c>authly_sk_</c> + 48 random URL-safe characters (Management API key).</summary>
    string GenerateApiKey();

    /// <summary>A numeric one-time passcode of the given length (default 6) for email/WhatsApp OTP.</summary>
    string GenerateNumericOtp(int digits = 6);

    /// <summary>A recovery code in the form <c>xxxxx-xxxxx</c> (lower-case Crockford-ish alphabet).</summary>
    string GenerateBackupCode();
}
