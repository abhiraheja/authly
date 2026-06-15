namespace Saarvix.Identity.Core.Enums;

/// <summary>Lifecycle state of a tenant (organization). Persisted as text.</summary>
public enum TenantStatus
{
    Active,
    Suspended,
    Deleted
}
