using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// Platform owner / operator. NOT tenant-scoped — super admins govern the whole
/// platform from an isolated surface with mandatory MFA. Maps to table "super_admins".
/// </summary>
public class SuperAdmin
{
    public Guid Id { get; set; }

    public string Email { get; set; } = default!;

    /// <summary>Argon2id hash.</summary>
    public string PasswordHash { get; set; } = default!;

    public SuperAdminRole Role { get; set; } = SuperAdminRole.Operator;

    /// <summary>MFA is mandatory for super admins (enforced from Phase 7).</summary>
    public bool MfaEnabled { get; set; } = true;

    /// <summary>True until the bootstrapped admin changes the seeded password.</summary>
    public bool MustChangePassword { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
}
