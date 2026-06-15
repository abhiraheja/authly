namespace Authly.Core.Enums;

/// <summary>Lifecycle state of an end-user account. Persisted as text.</summary>
public enum UserStatus
{
    Active,
    Suspended,
    Pending,
    Deleted
}
