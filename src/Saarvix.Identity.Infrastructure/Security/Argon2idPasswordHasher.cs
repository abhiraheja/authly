using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Saarvix.Identity.Core.Interfaces;

namespace Saarvix.Identity.Infrastructure.Security;

/// <summary>
/// Argon2id password hasher (memory-hard, GPU-resistant). Parameters follow the
/// OWASP minimum (m=19 MiB, t=2, p=1) and are embedded in the output so a hash
/// produced under one configuration still verifies after parameters change.
///
/// Encoded format (pipe-delimited): "argon2id|m|t|p|saltB64|hashB64".
/// </summary>
public sealed class Argon2idPasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;   // bytes
    private const int HashSize = 32;   // bytes

    // OWASP-recommended minimums for Argon2id.
    private const int MemoryKib = 19456; // 19 MiB
    private const int Iterations = 2;
    private const int Parallelism = 1;

    public string Hash(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt, MemoryKib, Iterations, Parallelism, HashSize);

        return string.Join('|',
            "argon2id",
            MemoryKib.ToString(),
            Iterations.ToString(),
            Parallelism.ToString(),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    public bool Verify(string encodedHash, string password)
    {
        if (string.IsNullOrEmpty(encodedHash) || password is null)
            return false;

        var parts = encodedHash.Split('|');
        if (parts.Length != 6 || parts[0] != "argon2id")
            return false;

        if (!int.TryParse(parts[1], out var memoryKib) ||
            !int.TryParse(parts[2], out var iterations) ||
            !int.TryParse(parts[3], out var parallelism))
            return false;

        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[4]);
            expected = Convert.FromBase64String(parts[5]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Derive(password, salt, memoryKib, iterations, parallelism, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    private static byte[] Derive(string password, byte[] salt, int memoryKib, int iterations, int parallelism, int hashSize)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memoryKib,
            Iterations = iterations,
            DegreeOfParallelism = parallelism
        };
        return argon2.GetBytes(hashSize);
    }
}
