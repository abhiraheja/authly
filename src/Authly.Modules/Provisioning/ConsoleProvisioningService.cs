using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Modules.Common;
using Authly.Modules.Tenants;

namespace Authly.Modules.Provisioning;

/// <summary>
/// Creates projects (tenants) inside an organization: slug de-duplication + end-user system-role
/// seeding, in one place. Used by self-serve signup (first project) and the console "New project"
/// flow (additional projects). Thrown when a unique slug cannot be derived.
/// </summary>
public sealed class ProjectProvisioningException : Exception
{
    public ProjectProvisioningException(string message) : base(message) { }
}

public interface IConsoleProvisioningService
{
    /// <summary>Creates a project in the org (de-duplicating its slug), binds the request's tenant
    /// context to it, and seeds its end-user (app) system roles. Returns the new project.</summary>
    Task<Tenant> CreateProjectAsync(Guid organizationId, string name, AuditContext actor, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ConsoleProvisioningService : IConsoleProvisioningService
{
    // Bound on slug de-duplication so a pathological name can't loop forever.
    private const int MaxSlugAttempts = 50;

    private readonly ITenantService _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IRbacService _rbac;

    public ConsoleProvisioningService(ITenantService tenants, ITenantContext tenantContext, IRbacService rbac)
    {
        _tenants = tenants;
        _tenantContext = tenantContext;
        _rbac = rbac;
    }

    public async Task<Tenant> CreateProjectAsync(Guid organizationId, string name, AuditContext actor, CancellationToken ct = default)
    {
        var baseSlug = TenantService.Slugify(name);
        Tenant? tenant = null;
        for (var attempt = 0; attempt < MaxSlugAttempts; attempt++)
        {
            // First attempt uses the natural slug; subsequent attempts disambiguate (acme, acme-2, …).
            var slug = attempt == 0 ? baseSlug : $"{baseSlug}-{attempt + 1}";
            try
            {
                tenant = await _tenants.CreateAsync(new CreateTenantRequest(name, slug, organizationId), actor, ct);
                break;
            }
            catch (SlugAlreadyExistsException)
            {
                // Try the next disambiguated slug.
            }
        }

        if (tenant is null)
            throw new ProjectProvisioningException("Could not allocate a unique project identifier. Please try a different name.");

        // Bind the (tenant-less) signup request to the new project so any later RLS writes are scoped.
        // Inside the console a tenant is already resolved (set-once) and switching is unnecessary —
        // role/permission seeding carries an explicit tenant_id and those tables are not RLS-protected.
        if (!_tenantContext.HasTenant)
            _tenantContext.SetTenant(tenant.Id);

        await _rbac.EnsureSystemRolesAsync(tenant.Id, ct);
        return tenant;
    }
}
