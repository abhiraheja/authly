using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Common;
using Authly.Modules.Signup;
using Authly.Modules.Tenants;
using AccountEntity = Authly.Core.Entities.Account;

namespace Authly.Tests.Signup;

public class TenantSignupTests
{
    private static (TenantSignupService svc, FakeAccountRepo accounts, FakeOrgRepo orgs,
        FakeMembershipRepo memberships, FakeTenantService tenants, FakeTenantContext ctx,
        FakeSignupRbac rbac, FakeOperatorRbac operatorRbac, FakeSignupAudit audit) Build()
    {
        var accounts = new FakeAccountRepo();
        var orgs = new FakeOrgRepo();
        var memberships = new FakeMembershipRepo();
        var tenants = new FakeTenantService();
        var ctx = new FakeTenantContext();
        var rbac = new FakeSignupRbac();
        var operatorRbac = new FakeOperatorRbac();
        var audit = new FakeSignupAudit();
        var hasher = new FakeHasher();
        var svc = new TenantSignupService(accounts, orgs, memberships, tenants, ctx, rbac, operatorRbac, hasher, audit);
        return (svc, accounts, orgs, memberships, tenants, ctx, rbac, operatorRbac, audit);
    }

    private static TenantSignupRequest Req(string company = "Acme Inc.", string email = "owner@acme.test")
        => new(company, email, "Sup3rSecret!", "Ada", "Lovelace");

    [Fact]
    public async Task Provisions_account_organization_project_and_membership()
    {
        var (svc, accounts, orgs, memberships, _, ctx, rbac, operatorRbac, audit) = Build();

        var result = await svc.SignUpAsync(Req(), new RequestInfo("1.2.3.4", "agent"));

        // Account (hashed password, owner email)
        Assert.Equal("owner@acme.test", result.Account.Email);
        Assert.False(string.IsNullOrEmpty(result.Account.PasswordHash));
        Assert.Contains(result.Account.Id, accounts.Added);

        // Organization owned by the account
        Assert.Equal("acme-inc", result.Organization.Slug);
        Assert.Equal(result.Account.Id, result.Organization.OwnerAccountId);
        Assert.Contains(result.Organization.Id, orgs.Added);

        // First project inside the org
        Assert.Equal("acme-inc", result.Tenant.Slug);
        Assert.Equal(result.Organization.Id, result.Tenant.OrganizationId);

        // Founding membership is immediately Active
        var membership = Assert.Single(memberships.Added);
        Assert.Equal(result.Account.Id, membership.AccountId);
        Assert.Equal(result.Organization.Id, membership.OrganizationId);
        Assert.Equal(MembershipStatus.Active, membership.Status);

        // End-user system roles seeded on the new project, with RLS bound to it
        Assert.Contains(result.Tenant.Id, rbac.SeededTenants);
        Assert.Equal(result.Tenant.Id, ctx.TenantId);

        // Operator RBAC seeded for the org + founder granted org_owner against their membership
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
        var (svc, accounts, _, _, _, _, _, _, _) = Build();
        accounts.TakenEmails.Add("owner@acme.test");

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() => svc.SignUpAsync(Req(), new RequestInfo(null, null)));
    }

    [Fact]
    public async Task Disambiguates_a_taken_project_slug()
    {
        var (svc, _, _, _, tenants, _, _, _, _) = Build();
        tenants.TakenSlugs.Add("acme-inc");        // first project slug already exists

        var result = await svc.SignUpAsync(Req(), new RequestInfo(null, null));

        Assert.Equal("acme-inc-2", result.Tenant.Slug);
    }

    [Fact]
    public async Task Disambiguates_a_taken_organization_slug()
    {
        var (svc, _, orgs, _, _, _, _, _, _) = Build();
        orgs.TakenSlugs.Add("acme-inc");           // first org slug already exists

        var result = await svc.SignUpAsync(Req(), new RequestInfo(null, null));

        Assert.Equal("acme-inc-2", result.Organization.Slug);
    }

    [Fact]
    public async Task Gives_up_with_a_clear_error_when_no_project_slug_is_free()
    {
        var (svc, _, _, _, tenants, _, _, _, _) = Build();
        tenants.RejectEverything = true;

        await Assert.ThrowsAsync<TenantSignupException>(() => svc.SignUpAsync(Req(), new RequestInfo(null, null)));
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
}

internal sealed class FakeSignupAudit : IAuditLogger
{
    public readonly List<string> Events = new();
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
    { Events.Add(@event); return Task.CompletedTask; }
}
