using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Common;
using Authly.Modules.Provisioning;
using Authly.Modules.Signup;
using Authly.Modules.Tenants;
using AccountEntity = Authly.Core.Entities.Account;

namespace Authly.Tests.Signup;

public class TenantSignupTests
{
    private static (TenantSignupService svc, FakeAccountRepo accounts, FakeOrgRepo orgs,
        FakeMembershipRepo memberships, FakeProvisioning provisioning, FakeOperatorRbac operatorRbac,
        FakeSignupAudit audit) Build()
    {
        var accounts = new FakeAccountRepo();
        var orgs = new FakeOrgRepo();
        var memberships = new FakeMembershipRepo();
        var provisioning = new FakeProvisioning();
        var operatorRbac = new FakeOperatorRbac();
        var audit = new FakeSignupAudit();
        var hasher = new FakeHasher();
        var svc = new TenantSignupService(accounts, orgs, memberships, provisioning, operatorRbac, hasher, audit);
        return (svc, accounts, orgs, memberships, provisioning, operatorRbac, audit);
    }

    private static TenantSignupRequest Req(string company = "Acme Inc.", string email = "owner@acme.test")
        => new(company, email, "Sup3rSecret!", "Ada", "Lovelace");

    [Fact]
    public async Task Provisions_account_organization_project_and_owner_membership()
    {
        var (svc, accounts, orgs, memberships, provisioning, operatorRbac, audit) = Build();

        var result = await svc.SignUpAsync(Req(), new RequestInfo("1.2.3.4", "agent"));

        // Account (hashed password, owner email)
        Assert.Equal("owner@acme.test", result.Account.Email);
        Assert.False(string.IsNullOrEmpty(result.Account.PasswordHash));
        Assert.Contains(result.Account.Id, accounts.Added);

        // Organization owned by the account
        Assert.Equal("acme-inc", result.Organization.Slug);
        Assert.Equal(result.Account.Id, result.Organization.OwnerAccountId);
        Assert.Contains(result.Organization.Id, orgs.Added);

        // First project provisioned inside the org
        Assert.Equal(result.Organization.Id, result.Tenant.OrganizationId);
        Assert.Contains((result.Organization.Id, "Acme Inc."), provisioning.Created);

        // Founding membership is immediately Active
        var membership = Assert.Single(memberships.Added);
        Assert.Equal(MembershipStatus.Active, membership.Status);

        // Operator RBAC seeded + founder granted org_owner
        Assert.Contains(result.Organization.Id, operatorRbac.SeededOrgs);
        Assert.Equal((membership.Id, "org_owner"), operatorRbac.Assignments.Single());

        // Audit trail
        Assert.Contains("account.created", audit.Events);
        Assert.Contains("organization.created", audit.Events);
        Assert.Contains("tenant.signup", audit.Events);
    }

    [Fact]
    public async Task Rejects_a_duplicate_account_email()
    {
        var (svc, accounts, _, _, _, _, _) = Build();
        accounts.TakenEmails.Add("owner@acme.test");

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => svc.SignUpAsync(Req(), new RequestInfo(null, null)));
    }

    [Fact]
    public async Task Disambiguates_a_taken_organization_slug()
    {
        var (svc, _, orgs, _, _, _, _) = Build();
        orgs.TakenSlugs.Add("acme-inc");           // first org slug already exists

        var result = await svc.SignUpAsync(Req(), new RequestInfo(null, null));

        Assert.Equal("acme-inc-2", result.Organization.Slug);
    }
}

public class ConsoleProvisioningTests
{
    private static (ConsoleProvisioningService svc, FakeTenantService tenants, FakeTenantContext ctx, FakeSignupRbac rbac) Build()
    {
        var tenants = new FakeTenantService();
        var ctx = new FakeTenantContext();
        var rbac = new FakeSignupRbac();
        return (new ConsoleProvisioningService(tenants, ctx, rbac), tenants, ctx, rbac);
    }

    [Fact]
    public async Task Creates_project_seeds_enduser_roles_and_binds_context()
    {
        var (svc, _, ctx, rbac) = Build();
        var orgId = Guid.NewGuid();

        var project = await svc.CreateProjectAsync(orgId, "Acme Inc.", AuditContext.System);

        Assert.Equal("acme-inc", project.Slug);
        Assert.Equal(orgId, project.OrganizationId);
        Assert.Contains(project.Id, rbac.SeededTenants);   // end-user system roles seeded
        Assert.Equal(project.Id, ctx.TenantId);            // RLS context bound to the new project
    }

    [Fact]
    public async Task Disambiguates_a_taken_project_slug()
    {
        var (svc, tenants, _, _) = Build();
        tenants.TakenSlugs.Add("acme-inc");

        var project = await svc.CreateProjectAsync(Guid.NewGuid(), "Acme Inc.", AuditContext.System);

        Assert.Equal("acme-inc-2", project.Slug);
    }

    [Fact]
    public async Task Throws_when_no_slug_is_free()
    {
        var (svc, tenants, _, _) = Build();
        tenants.RejectEverything = true;

        await Assert.ThrowsAsync<ProjectProvisioningException>(() => svc.CreateProjectAsync(Guid.NewGuid(), "Acme", AuditContext.System));
    }
}

// --- Fakes (interface-focused; unused members throw to surface accidental use) ---

internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;
    public bool Verify(string encodedHash, string password) => encodedHash == "hash:" + password;
}

internal sealed class FakeTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public bool HasTenant => TenantId.HasValue;
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}

internal sealed class FakeProvisioning : IConsoleProvisioningService
{
    public readonly List<(Guid OrgId, string Name)> Created = new();
    public Task<Tenant> CreateProjectAsync(Guid organizationId, string name, AuditContext actor, CancellationToken ct = default)
    {
        Created.Add((organizationId, name));
        return Task.FromResult(new Tenant
        {
            Id = Guid.NewGuid(), Name = name, Slug = TenantService.Slugify(name),
            OrganizationId = organizationId, Status = TenantStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
    }
}

internal sealed class FakeAccountRepo : IAccountRepository
{
    public readonly HashSet<string> TakenEmails = new();
    public readonly List<Guid> Added = new();

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => Task.FromResult(TakenEmails.Contains(email));

    public Task AddAsync(AccountEntity account, CancellationToken ct = default)
    {
        if (account.Id == Guid.Empty) account.Id = Guid.NewGuid();
        Added.Add(account.Id);
        TakenEmails.Add(account.Email);
        return Task.CompletedTask;
    }

    public Task<AccountEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<AccountEntity?> GetByEmailAsync(string email, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(AccountEntity account, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeOrgRepo : IOrganizationRepository
{
    public readonly HashSet<string> TakenSlugs = new();
    public readonly List<Guid> Added = new();

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => Task.FromResult(TakenSlugs.Contains(slug));

    public Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        if (organization.Id == Guid.Empty) organization.Id = Guid.NewGuid();
        Added.Add(organization.Id);
        TakenSlugs.Add(organization.Slug);
        return Task.CompletedTask;
    }

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Organization?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(Organization organization, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(Organization organization, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeMembershipRepo : IOrganizationMembershipRepository
{
    public readonly List<OrganizationMembership> Added = new();

    public Task AddAsync(OrganizationMembership membership, CancellationToken ct = default)
    {
        if (membership.Id == Guid.Empty) membership.Id = Guid.NewGuid();
        Added.Add(membership);
        return Task.CompletedTask;
    }

    public Task<OrganizationMembership?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<OrganizationMembership?> GetAsync(Guid accountId, Guid organizationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<OrganizationMembership>> ListByAccountAsync(Guid accountId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationWithAccountsAsync(Guid organizationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(OrganizationMembership membership, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeTenantService : ITenantService
{
    public readonly HashSet<string> TakenSlugs = new();
    public bool RejectEverything;

    public Task<Tenant> CreateAsync(CreateTenantRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var slug = request.Slug!;
        if (RejectEverything || !TakenSlugs.Add(slug))
            throw new SlugAlreadyExistsException(slug);
        return Task.FromResult(new Tenant
        {
            Id = Guid.NewGuid(), Name = request.Name, Slug = slug,
            OrganizationId = request.OrganizationId ?? throw new InvalidOperationException("OrganizationId required"),
            Status = TenantStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Tenant?> GetAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SuspendAsync(Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task ReactivateAsync(Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> IsOnboardedAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetOnboardedAsync(Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeSignupRbac : IRbacService
{
    public readonly List<Guid> SeededTenants = new();

    public Task EnsureSystemRolesAsync(Guid tenantId, CancellationToken ct = default) { SeededTenants.Add(tenantId); return Task.CompletedTask; }

    public Task AssignRoleAsync(Guid tenantId, Guid userId, Guid roleId, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Role>> ListRolesAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<RoleWithPermissions?> GetRoleAsync(Guid t, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Role> CreateRoleAsync(Guid t, CreateRoleRequest r, AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetRolePermissionsAsync(Guid t, Guid r, IReadOnlyCollection<Guid> p, AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteRoleAsync(Guid t, Guid r, AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Role>> ListUserRolesAsync(Guid t, Guid u, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRoleAsync(Guid t, Guid u, Guid r, AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<UserAuthorization> GetUserAuthorizationAsync(Guid t, Guid u, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeOperatorRbac : Authly.Modules.Operators.IOperatorRbacService
{
    public readonly List<Guid> SeededOrgs = new();
    public readonly List<(Guid MembershipId, string Role)> Assignments = new();

    public Task EnsureSystemRolesAsync(Guid organizationId, CancellationToken ct = default) { SeededOrgs.Add(organizationId); return Task.CompletedTask; }
    public Task AssignSystemRoleAsync(Guid organizationId, Guid membershipId, string roleName, Guid? grantedByAccountId, CancellationToken ct = default)
    { Assignments.Add((membershipId, roleName)); return Task.CompletedTask; }

    public Task<IReadOnlyList<Authly.Core.Entities.OperatorRole>> ListRolesAsync(Guid organizationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Authly.Modules.Operators.OperatorRoleWithPermissions?> GetRoleAsync(Guid organizationId, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Authly.Core.Entities.OperatorRole> CreateRoleAsync(Guid organizationId, Authly.Modules.Authorization.CreateRoleRequest request, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetRolePermissionsAsync(Guid organizationId, Guid roleId, IReadOnlyCollection<Guid> permissionIds, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteRoleAsync(Guid organizationId, Guid roleId, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Authly.Core.Entities.OperatorPermission>> ListPermissionsAsync(Guid organizationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Authly.Core.Entities.OperatorRole>> ListMemberRolesAsync(Guid membershipId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AssignRoleToMemberAsync(Guid organizationId, Guid membershipId, Guid roleId, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRoleFromMemberAsync(Guid organizationId, Guid membershipId, Guid roleId, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeSignupAudit : IAuditLogger
{
    public readonly List<string> Events = new();
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
    { Events.Add(@event); return Task.CompletedTask; }
}
