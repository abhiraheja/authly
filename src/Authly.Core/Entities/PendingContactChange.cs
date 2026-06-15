using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A two-step, cancellable change of a user's email or phone. The new contact must be verified
/// (via <see cref="VerifyTokenHash"/>) before it is applied; meanwhile the OLD contact is notified
/// and can abort the change with <see cref="CancelTokenHash"/>. Only SHA-256 hashes of the tokens
/// are stored. Tenant-scoped. Maps to table "pending_contact_changes".
/// </summary>
public class PendingContactChange
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }

    public ContactType ChangeType { get; set; }

    /// <summary>The requested new email/phone.</summary>
    public string NewValue { get; set; } = default!;

    /// <summary>SHA-256 of the token sent to the NEW contact to confirm ownership.</summary>
    public string VerifyTokenHash { get; set; } = default!;

    /// <summary>SHA-256 of the token in the alert sent to the OLD contact to abort the change.</summary>
    public string CancelTokenHash { get; set; } = default!;

    public ContactChangeStatus Status { get; set; } = ContactChangeStatus.Pending;

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
