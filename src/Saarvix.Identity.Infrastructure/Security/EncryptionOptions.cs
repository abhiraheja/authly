namespace Saarvix.Identity.Infrastructure.Security;

/// <summary>Configuration for <see cref="AesEncryptionService"/>.</summary>
public sealed class EncryptionOptions
{
    /// <summary>Base64-encoded 32-byte (256-bit) key. Supplied via ENCRYPTION_KEY env/vault.</summary>
    public string Key { get; set; } = default!;
}
