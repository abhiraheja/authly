namespace Authly.Core.Enums;

/// <summary>The kind of contact a recovery contact or a pending contact change refers to.</summary>
public enum ContactType
{
    Email,
    Phone
}

/// <summary>Lifecycle of a pending email/phone change.</summary>
public enum ContactChangeStatus
{
    /// <summary>Awaiting verification of the new contact (cancellable from the old contact).</summary>
    Pending,

    /// <summary>The new contact was verified and applied.</summary>
    Completed,

    /// <summary>The change was cancelled (by the user from the old contact, or it expired).</summary>
    Cancelled
}
