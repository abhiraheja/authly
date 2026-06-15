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

    public string GenerateClientId() => "client_" + RandomString(24);
    public string GenerateClientSecret() => "secret_" + RandomString(48);
    public string GenerateApiKey() => "authly_sk_" + RandomString(48);

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
