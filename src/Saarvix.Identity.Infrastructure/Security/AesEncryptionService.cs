using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Saarvix.Identity.Core.Interfaces;

namespace Saarvix.Identity.Infrastructure.Security;

/// <summary>
/// AES-256-GCM authenticated encryption for reversible secrets (TOTP seeds, social
/// tokens, provider keys). Output is base64 of [nonce(12) | tag(16) | ciphertext].
/// A fresh random nonce is generated per call.
/// </summary>
public sealed class AesEncryptionService : IEncryptionService
{
    private const int NonceSize = 12; // AES-GCM standard nonce
    private const int TagSize = 16;   // 128-bit auth tag

    private readonly byte[] _key;

    public AesEncryptionService(IOptions<EncryptionOptions> options)
    {
        var raw = options.Value.Key
            ?? throw new InvalidOperationException("ENCRYPTION_KEY is not configured.");

        _key = Convert.FromBase64String(raw);
        if (_key.Length != 32)
            throw new InvalidOperationException("ENCRYPTION_KEY must decode to 32 bytes (256-bit).");
    }

    public string Encrypt(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var plainBytes = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipher = new byte[plainBytes.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plainBytes, cipher, tag);

        var output = new byte[NonceSize + TagSize + cipher.Length];
        Buffer.BlockCopy(nonce, 0, output, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, output, NonceSize, TagSize);
        Buffer.BlockCopy(cipher, 0, output, NonceSize + TagSize, cipher.Length);

        return Convert.ToBase64String(output);
    }

    public string Decrypt(string encrypted)
    {
        ArgumentNullException.ThrowIfNull(encrypted);

        var input = Convert.FromBase64String(encrypted);
        if (input.Length < NonceSize + TagSize)
            throw new CryptographicException("Ciphertext is too short to be valid.");

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipher = new byte[input.Length - NonceSize - TagSize];

        Buffer.BlockCopy(input, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(input, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(input, NonceSize + TagSize, cipher, 0, cipher.Length);

        var plain = new byte[cipher.Length];
        using var aes = new AesGcm(_key, TagSize);
        aes.Decrypt(nonce, cipher, tag, plain);

        return Encoding.UTF8.GetString(plain);
    }
}
