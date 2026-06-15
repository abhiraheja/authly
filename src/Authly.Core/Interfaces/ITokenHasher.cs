namespace Authly.Core.Interfaces;

/// <summary>
/// One-way hashing for opaque, high-entropy tokens (email/reset/session tokens) where a
/// fast deterministic digest is required for lookup. Implemented with SHA-256 in
/// Infrastructure. NOT for passwords — use <see cref="IPasswordHasher"/> for those.
/// </summary>
public interface ITokenHasher
{
    /// <summary>Generates a new URL-safe, high-entropy raw token (returned to the caller, never stored).</summary>
    string GenerateRawToken();

    /// <summary>Computes the stable hash of a raw token for storage/lookup.</summary>
    string Hash(string rawToken);
}
