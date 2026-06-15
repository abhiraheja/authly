using System.Security.Cryptography;
using System.Text;
using Authly.Core.Interfaces;

namespace Authly.Infrastructure.Security;

/// <summary>
/// CSPRNG-based generator for client ids (<c>client_[24]</c>) and secrets (<c>secret_[48]</c>).
/// Characters are drawn uniformly (rejection sampling) from a 64-char URL-safe alphabet, so the
/// output is unambiguous and copy-paste safe.
/// </summary>
public sealed class CredentialGenerator : ICredentialGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";

    // Lower-case, digit-only-ambiguity-free alphabet for human-typed recovery codes (no 0/1/o/l).
    private const string BackupAlphabet = "abcdefghijkmnpqrstuvwxyz23456789";

    public string GenerateClientId() => "client_" + RandomString(24);
    public string GenerateClientSecret() => "secret_" + RandomString(48);
    public string GenerateApiKey() => "authly_sk_" + RandomString(48);

    public string GenerateNumericOtp(int digits = 6)
    {
        if (digits is < 4 or > 10) throw new ArgumentOutOfRangeException(nameof(digits));
        var sb = new StringBuilder(digits);
        for (var i = 0; i < digits; i++)
            sb.Append((char)('0' + RandomNumberGenerator.GetInt32(10)));
        return sb.ToString();
    }

    public string GenerateBackupCode()
    {
        // Two groups of five characters from a 32-char (5-bit) alphabet → ~50 bits of entropy.
        var bytes = RandomNumberGenerator.GetBytes(10);
        var sb = new StringBuilder(11);
        for (var i = 0; i < 10; i++)
        {
            if (i == 5) sb.Append('-');
            sb.Append(BackupAlphabet[bytes[i] & 0x1F]);
        }
        return sb.ToString();
    }

    private static string RandomString(int length)
    {
        // Alphabet length is 64, a power of two, so masking 6 bits gives a uniform index
        // with no modulo bias — every byte yields a valid character.
        var bytes = RandomNumberGenerator.GetBytes(length);
        var sb = new StringBuilder(length);
        foreach (var b in bytes)
            sb.Append(Alphabet[b & 0x3F]);
        return sb.ToString();
    }
}
