namespace Authly.Core.Interfaces;

/// <summary>
/// Password hashing abstraction. Implemented with Argon2id in Infrastructure.
/// The resulting hash is self-describing (parameters embedded) so verification
/// does not depend on current configuration.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Hashes a plaintext password, returning an encoded, self-describing hash.</summary>
    string Hash(string password);

    /// <summary>Verifies a plaintext password against a previously produced hash.</summary>
    bool Verify(string encodedHash, string password);
}
