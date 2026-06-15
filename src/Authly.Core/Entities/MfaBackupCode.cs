namespace Authly.Core.Entities;

/// <summary>
/// A single-use recovery code. Only the hash is stored (like a password); the raw codes are
/// shown to the user exactly once at generation. Looked up by user; carries no tenant_id of its
/// own (protected transitively — it is only ever queried for a user already scoped to a tenant).
/// Maps to table "mfa_backup_codes".
/// </summary>
public class MfaBackupCode
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 of the normalized raw code.</summary>
    public string CodeHash { get; set; } = default!;

    public bool Used { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
