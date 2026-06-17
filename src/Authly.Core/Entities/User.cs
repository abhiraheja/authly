using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// An end-user belonging to a tenant. Email is unique PER tenant, not globally —
/// the same person can have separate accounts across tenants. Maps to table "users".
/// </summary>
public class User
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Email { get; set; } = default!;
    public bool EmailVerified { get; set; }

    public string? Username { get; set; }

    public string? Phone { get; set; }
    public bool PhoneVerified { get; set; }

    /// <summary>Argon2id hash. NULL for social-only accounts.</summary>
    public string? PasswordHash { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>Guest / anonymous auth — upgradeable to a full account later.</summary>
    public bool IsAnonymous { get; set; }

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? AvatarUrl { get; set; }
    public string Timezone { get; set; } = "UTC";
    public string Locale { get; set; } = "en";

    /// <summary>User CAN edit (display name, avatar preference). JSONB.</summary>
    public string UserMetadata { get; set; } = "{}";

    /// <summary>Only backend CAN edit (plan, roles). Never user-editable. JSONB.</summary>
    public string AppMetadata { get; set; } = "{}";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public Tenant Tenant { get; set; } = default!;
}
