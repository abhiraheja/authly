namespace Authly.Core.Interfaces;

/// <summary>
/// Per-request accessor for the current tenant. Set by tenant-resolution middleware
/// and used both by the application (every tenant-scoped query filters on it) and by
/// the DB connection (sets <c>app.current_tenant</c> for the RLS backstop).
/// Null when no tenant is in scope (e.g. super-admin surface).
/// </summary>
public interface ITenantContext
{
    Guid? TenantId { get; }
    bool HasTenant { get; }
    void SetTenant(Guid tenantId);
}
