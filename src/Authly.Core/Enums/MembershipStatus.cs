namespace Authly.Core.Enums;

/// <summary>State of an <see cref="Entities.OrganizationMembership"/> linking an Account to an Organization. Persisted as text.</summary>
public enum MembershipStatus
{
    /// <summary>Invited but not yet accepted — cannot operate the console until accepted.</summary>
    Invited,

    /// <summary>Accepted and able to operate the org's console (subject to operator RBAC).</summary>
    Active,

    /// <summary>Membership revoked/suspended — access denied.</summary>
    Disabled
}
