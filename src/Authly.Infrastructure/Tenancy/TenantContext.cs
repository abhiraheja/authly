using Authly.Core.Interfaces;

namespace Authly.Infrastructure.Tenancy;

/// <summary>
/// Scoped, mutable holder for the current request's tenant. Populated by tenant-resolution
/// middleware. Once set it cannot be reassigned to a different tenant within the same scope.
/// </summary>
public sealed class TenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public bool HasTenant => TenantId.HasValue;

    public void SetTenant(Guid tenantId)
    {
        if (TenantId.HasValue && TenantId.Value != tenantId)
            throw new InvalidOperationException("The tenant for this request has already been set.");
        TenantId = tenantId;
    }
}
