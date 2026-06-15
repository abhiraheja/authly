namespace Authly.Core.Enums;

/// <summary>Lifecycle of an MFA factor. A factor is only usable for login once <c>Active</c>.</summary>
public enum MfaFactorStatus
{
    /// <summary>Enrolled but not yet verified (e.g. TOTP secret issued, first code not confirmed).</summary>
    Pending,
    Active,
    Revoked
}
