using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Common;
using Authly.Modules.Tenants;

namespace Authly.Modules.Signup;

/// <summary>Self-service signup input: a new workspace plus its first administrator.</summary>
public sealed record TenantSignupRequest(
    string CompanyName,
    string Email,
    string Password,
    string? FirstName = null,
    string? LastName = null);

/// <summary>The provisioned workspace and its bootstrap admin user.</summary>
public sealed record TenantSignupResult(Tenant Tenant, User User);

/// <summary>Thrown when a workspace cannot be provisioned (e.g. a unique slug could not be derived).</summary>
public sealed class TenantSignupException : Exception
{
    public TenantSignupException(string message) : base(message) { }
}

/// <summary>
/// Public, self-service tenant onboarding (the Supabase / Google-Console model): a visitor creates
/// a workspace and becomes its first administrator in one step — no super-admin involvement.
/// </summary>
public interface ITenantSignupService
{
    Task<TenantSignupResult> SignUpAsync(TenantSignupRequest request, RequestInfo info, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class TenantSignupService : ITenantSignupService
{
    // Bound on slug de-duplication so a pathological name can't loop forever.
    private const int MaxSlugAttempts = 50;

    private readonly ITenantService _tenants;
    private readonly ITenantContext _tenantContext;
    private readonly IAuthService _auth;
    private readonly IUserRepository _users;
    private readonly IRbacService _rbac;
    private readonly IRoleRepository _roles;
    private readonly IAuditLogger _audit;

    public TenantSignupService(
        ITenantService tenants,
        ITenantContext tenantContext,
        IAuthService auth,
        IUserRepository users,
        IRbacService rbac,
        IRoleRepository roles,
        IAuditLogger audit)
    {
        _tenants = tenants;
        _tenantContext = tenantContext;
        _auth = auth;
        _users = users;
        _rbac = rbac;
        _roles = roles;
        _audit = audit;
    }

    public async Task<TenantSignupResult> SignUpAsync(TenantSignupRequest request, RequestInfo info, CancellationToken ct = default)
    {
        // 1) Create the workspace. The signup surface is tenant-less, so this runs without an
        //    ambient tenant (the tenants table is not RLS-protected). De-duplicate the slug since
        //    two visitors may pick the same company name.
        var tenant = await CreateWorkspaceAsync(request.CompanyName, ct);

        // 2) Bind the request to the new tenant so the RLS-protected writes below are permitted
        //    (the connection interceptor pushes this into app.current_tenant).
        _tenantContext.SetTenant(tenant.Id);

        // 3) Provision the first user inside the new workspace.
        var user = await _auth.RegisterAsync(tenant.Id,
            new RegisterRequest(request.Email, request.Password, request.FirstName, request.LastName), info, ct);

        // 4) Promote that user to tenant admin and grant the seeded tenant_admin role so their
        //    tokens carry full permissions — mirrors the first-admin bootstrap in TenantAdminService.
        var actor = new AuditContext(user.Id, "user", info.IpAddress, info.UserAgent);
        user.IsTenantAdmin = true;
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user, ct);

        await _rbac.EnsureSystemRolesAsync(tenant.Id, ct);
        if (await _roles.GetRoleByNameAsync(tenant.Id, SystemRbac.TenantAdmin, ct) is { } adminRole)
            await _rbac.AssignRoleAsync(tenant.Id, user.Id, adminRole.Id, actor, ct);

        await _audit.LogAsync("tenant.signup", actor, tenant.Id,
            resourceType: "tenant", resourceId: tenant.Id,
            metadata: new { tenant.Slug, tenant.Name }, ct: ct);

        return new TenantSignupResult(tenant, user);
    }

    private async Task<Tenant> CreateWorkspaceAsync(string companyName, CancellationToken ct)
    {
        var baseSlug = TenantService.Slugify(companyName);
        for (var attempt = 0; attempt < MaxSlugAttempts; attempt++)
        {
            // First attempt uses the natural slug; subsequent attempts disambiguate (acme, acme-2, …).
            var slug = attempt == 0 ? baseSlug : $"{baseSlug}-{attempt + 1}";
            try
            {
                return await _tenants.CreateAsync(
                    new CreateTenantRequest(companyName, slug), AuditContext.System, ct);
            }
            catch (SlugAlreadyExistsException)
            {
                // Try the next disambiguated slug.
            }
        }

        throw new TenantSignupException("Could not allocate a unique workspace identifier. Please try a different company name.");
    }
}
