using System.Security.Cryptography;
using Authly.Core.Interfaces;

namespace Authly.Infrastructure.Security;

/// <summary>
/// Generates high-entropy opaque tokens and hashes them with SHA-256 for storage/lookup.
/// SHA-256 is appropriate here (and not for passwords) because the input is already
/// 256 bits of cryptographic randomness — there is nothing to brute-force, so the slow,
/// salted Argon2id treatment used for low-entropy passwords is unnecessary.
/// </summary>
public sealed class Sha256TokenHasher : ITokenHasher
{
    public string GenerateRawToken()
    {
        // 32 bytes = 256 bits of entropy, URL-safe Base64 (no padding/+//).
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    public string Hash(string rawToken)
    {
        var digest = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(digest);
    }
}
