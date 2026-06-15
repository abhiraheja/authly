using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// An out-of-band channel a user can be reached at for account recovery (a secondary email or a
/// phone). Tenant-scoped. When a recovery is initiated, every verified contact is notified. Maps
/// to table "recovery_contacts".
/// </summary>
public class RecoveryContact
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public ContactType Type { get; set; }

    /// <summary>The email address or phone number.</summary>
    public string Value { get; set; } = default!;

    /// <summary>True once the owner has proven control of this contact.</summary>
    public bool Verified { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
