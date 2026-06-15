namespace Saarvix.Identity.Core.Interfaces;

/// <summary>
/// Symmetric encryption for data that must be reversible but never stored in clear
/// (TOTP secrets, social tokens, provider API keys, webhook/HMAC secrets).
/// Implemented with AES-256-GCM in Infrastructure.
/// </summary>
public interface IEncryptionService
{
    /// <summary>Encrypts UTF-8 plaintext, returning a base64 token (nonce + tag + ciphertext).</summary>
    string Encrypt(string plaintext);

    /// <summary>Decrypts a base64 token produced by <see cref="Encrypt"/>.</summary>
    string Decrypt(string encrypted);
}
