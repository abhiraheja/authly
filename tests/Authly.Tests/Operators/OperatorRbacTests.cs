using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Modules.Operators;

namespace Authly.Tests.Operators;

public class OperatorRbacServiceTests
{
    [Fact]
    public async Task EnsureSystemRoles_seeds_catalogue_and_roles_and_is_idempotent()
    {
        var roles = new InMemoryOperatorRoleRepo();
        var svc = new OperatorRbacService(roles, new InMemoryMemberRoleRepo(), new NoopAudit());
        var orgId = Guid.NewGuid();

        await svc.EnsureSystemRolesAsync(orgId);
        var permCount = roles.Permissions.Count;
        var roleCount = roles.Roles.Count;

        Assert.Equal(OperatorRbac.Permissions.Count, permCount);
        Assert.Equal(OperatorRbac.RoleNames.Count, roleCount);

        // org_owner gets the full catalogue; viewer only *.read.
        var owner = roles.Roles.Single(r => r.Name == "org_owner");
        Assert.Equal(permCount, roles.RolePermissions[owner.Id].Count);
        var viewer = roles.Roles.Single(r => r.Name == "viewer");
        Assert.All(roles.RolePermissions[viewer.Id], pid => Assert.Equal("read", roles.Permissions.Single(p => p.Id == pid).Action));

        // Re-seeding does not duplicate.
        await svc.EnsureSystemRolesAsync(orgId);
        Assert.Equal(permCount, roles.Permissions.Count);
        Assert.Equal(roleCount, roles.Roles.Count);
    }
}

public class OperatorRoleManagementTests
{
    private static (OperatorRbacService svc, InMemoryOperatorRoleRepo roles, InMemoryMemberRoleRepo memberRoles, Guid orgId) Build()
    {
        var roles = new InMemoryOperatorRoleRepo();
        var memberRoles = new InMemoryMemberRoleRepo();
        return (new OperatorRbacService(roles, memberRoles, new NoopAudit()), roles, memberRoles, Guid.NewGuid());
    }

    [Fact]
    public async Task Create_role_then_set_permissions_filters_foreign_ids()
    {
        var (svc, roles, _, org) = Build();
        await svc.EnsureSystemRolesAsync(org);

        var role = await svc.CreateRoleAsync(org, new CreateRoleRequest("support", "Support staff"), AuditContextStub());
        Assert.False(role.IsSystem);

        var orgPerm = roles.Permissions.First(p => p.OrganizationId == org).Id;
        await svc.SetRolePermissionsAsync(org, role.Id, new[] { orgPerm, Guid.NewGuid() }, AuditContextStub());

        var saved = await svc.GetRoleAsync(org, role.Id);
        Assert.NotNull(saved);
        Assert.Equal(new[] { orgPerm }, saved!.PermissionIds);
    }

    [Fact]
    public async Task Create_role_with_duplicate_name_throws()
    {
        var (svc, _, _, org) = Build();
        await svc.CreateRoleAsync(org, new CreateRoleRequest("support", null), AuditContextStub());
        await Assert.ThrowsAsync<RoleNameAlreadyExistsException>(
            () => svc.CreateRoleAsync(org, new CreateRoleRequest("support", null), AuditContextStub()));
    }

    [Fact]
    public async Task Delete_system_role_is_protected()
    {
        var (svc, roles, _, org) = Build();
        await svc.EnsureSystemRolesAsync(org);
        var owner = roles.Roles.Single(r => r.Name == OperatorRbac.OrgOwner);
        await Assert.ThrowsAsync<SystemRoleProtectedException>(() => svc.DeleteRoleAsync(org, owner.Id, AuditContextStub()));
    }

    [Fact]
    public async Task Assign_and_remove_member_role()
    {
        var (svc, roles, memberRoles, org) = Build();
        await svc.EnsureSystemRolesAsync(org);
        var viewer = roles.Roles.Single(r => r.Name == OperatorRbac.Viewer);
        var membershipId = Guid.NewGuid();

        await svc.AssignRoleToMemberAsync(org, membershipId, viewer.Id, AuditContextStub());
        Assert.Single(memberRoles.Assignments, a => a.OrganizationMembershipId == membershipId && a.OperatorRoleId == viewer.Id);

        await svc.RemoveRoleFromMemberAsync(org, membershipId, viewer.Id, AuditContextStub());
        Assert.DoesNotContain(memberRoles.Assignments, a => a.OrganizationMembershipId == membershipId && a.OperatorRoleId == viewer.Id);
    }

    [Fact]
    public async Task Cannot_strip_the_last_owner_role()
    {
        var (svc, roles, _, org) = Build();
        await svc.EnsureSystemRolesAsync(org);
        var owner = roles.Roles.Single(r => r.Name == OperatorRbac.OrgOwner);
        var onlyOwner = Guid.NewGuid();

        await svc.AssignRoleToMemberAsync(org, onlyOwner, owner.Id, AuditContextStub());
        await Assert.ThrowsAsync<LastOwnerProtectedException>(
            () => svc.RemoveRoleFromMemberAsync(org, onlyOwner, owner.Id, AuditContextStub()));
    }

    [Fact]
    public async Task Can_strip_owner_role_when_another_owner_exists()
    {
        var (svc, roles, _, org) = Build();
        await svc.EnsureSystemRolesAsync(org);
        var owner = roles.Roles.Single(r => r.Name == OperatorRbac.OrgOwner);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        await svc.AssignRoleToMemberAsync(org, a, owner.Id, AuditContextStub());
        await svc.AssignRoleToMemberAsync(org, b, owner.Id, AuditContextStub());

        // Two owners → one can be demoted.
        await svc.RemoveRoleFromMemberAsync(org, a, owner.Id, AuditContextStub());
    }

    private static Authly.Modules.Common.AuditContext AuditContextStub() => Authly.Modules.Common.AuditContext.System;
}

public class ConsoleAccessServiceTests
{
    private static (ConsoleAccessService svc, Guid accountId, Guid orgId, Guid projectId) Build(
        MembershipStatus? membershipStatus, bool projectInOrg = true)
    {
        var accountId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var memberships = new StubMembershipRepo(membershipStatus is { } s
            ? new OrganizationMembership { Id = Guid.NewGuid(), AccountId = accountId, OrganizationId = orgId, Status = s }
            : null);
        var tenants = new StubTenantRepo(new Tenant { Id = projectId, OrganizationId = projectInOrg ? orgId : Guid.NewGuid() });
        var memberRoles = new InMemoryMemberRoleRepo();
        return (new ConsoleAccessService(memberships, tenants, memberRoles), accountId, orgId, projectId);
    }

    [Fact]
    public async Task Resolves_for_active_member_with_project_in_org()
    {
        var (svc, acc, org, proj) = Build(MembershipStatus.Active);
        Assert.NotNull(await svc.ResolveAsync(acc, org, proj));
    }

    [Fact]
    public async Task Denies_non_member()
    {
        var (svc, acc, org, proj) = Build(membershipStatus: null);
        Assert.Null(await svc.ResolveAsync(acc, org, proj));
    }

    [Fact]
    public async Task Denies_disabled_member()
    {
        var (svc, acc, org, proj) = Build(MembershipStatus.Disabled);
        Assert.Null(await svc.ResolveAsync(acc, org, proj));
    }

    [Fact]
    public async Task Denies_when_project_not_in_org()
    {
        var (svc, acc, org, proj) = Build(MembershipStatus.Active, projectInOrg: false);
        Assert.Null(await svc.ResolveAsync(acc, org, proj));
    }
}

internal sealed class NoopAudit : Authly.Modules.Audit.IAuditLogger
{
    public Task LogAsync(string @event, Authly.Modules.Common.AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
        => Task.CompletedTask;
}

// --- In-memory / stub repos ---

internal sealed class InMemoryOperatorRoleRepo : IOperatorRoleRepository
{
    public readonly List<OperatorRole> Roles = new();
    public readonly List<OperatorPermission> Permissions = new();
    public readonly Dictionary<Guid, List<Guid>> RolePermissions = new();

    public Task<IReadOnlyList<OperatorRole>> ListRolesAsync(Guid org, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OperatorRole>>(Roles.Where(r => r.OrganizationId == org).ToList());
    public Task<OperatorRole?> GetRoleAsync(Guid org, Guid id, CancellationToken ct = default)
        => Task.FromResult(Roles.FirstOrDefault(r => r.OrganizationId == org && r.Id == id));
    public Task<OperatorRole?> GetRoleByNameAsync(Guid org, string name, CancellationToken ct = default)
        => Task.FromResult(Roles.FirstOrDefault(r => r.OrganizationId == org && r.Name == name));
    public Task AddRoleAsync(OperatorRole role, CancellationToken ct = default)
    { if (role.Id == Guid.Empty) role.Id = Guid.NewGuid(); Roles.Add(role); return Task.CompletedTask; }
    public Task UpdateRoleAsync(OperatorRole role, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteRoleAsync(OperatorRole role, CancellationToken ct = default) { Roles.Remove(role); return Task.CompletedTask; }
    public Task<IReadOnlyList<OperatorPermission>> ListPermissionsAsync(Guid org, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OperatorPermission>>(Permissions.Where(p => p.OrganizationId == org).ToList());
    public Task AddPermissionAsync(OperatorPermission permission, CancellationToken ct = default)
    { if (permission.Id == Guid.Empty) permission.Id = Guid.NewGuid(); Permissions.Add(permission); return Task.CompletedTask; }
    public Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(RolePermissions.TryGetValue(roleId, out var l) ? l : new List<Guid>());
    public Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default)
    { RolePermissions[roleId] = permissionIds.Distinct().ToList(); return Task.CompletedTask; }
}

internal sealed class InMemoryMemberRoleRepo : IMemberRoleRepository
{
    public readonly List<MemberRole> Assignments = new();
    public Task AssignAsync(MemberRole a, CancellationToken ct = default) { Assignments.Add(a); return Task.CompletedTask; }
    public Task RemoveAsync(Guid membershipId, Guid roleId, CancellationToken ct = default)
    { Assignments.RemoveAll(m => m.OrganizationMembershipId == membershipId && m.OperatorRoleId == roleId); return Task.CompletedTask; }
    public Task RemoveAllForMembershipAsync(Guid membershipId, CancellationToken ct = default)
    { Assignments.RemoveAll(m => m.OrganizationMembershipId == membershipId); return Task.CompletedTask; }
    public Task<IReadOnlyList<OperatorRole>> ListRolesForMembershipAsync(Guid membershipId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OperatorRole>>(Assignments.Where(m => m.OrganizationMembershipId == membershipId).Select(m => m.OperatorRole).ToList());
    public Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid membershipId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    public Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid membershipId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    public Task<int> CountMembershipsWithRoleAsync(Guid org, Guid roleId, CancellationToken ct = default)
        => Task.FromResult(Assignments.Where(m => m.OrganizationId == org && m.OperatorRoleId == roleId).Select(m => m.OrganizationMembershipId).Distinct().Count());
}

internal sealed class StubMembershipRepo : IOrganizationMembershipRepository
{
    private readonly OrganizationMembership? _m;
    public StubMembershipRepo(OrganizationMembership? m) => _m = m;
    public Task<OrganizationMembership?> GetAsync(Guid accountId, Guid organizationId, CancellationToken ct = default) => Task.FromResult(_m);
    public Task<OrganizationMembership?> GetByIdAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<OrganizationMembership>> ListByAccountAsync(Guid accountId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationWithAccountsAsync(Guid organizationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddAsync(OrganizationMembership m, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(OrganizationMembership m, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class StubTenantRepo : ITenantRepository
{
    private readonly Tenant _t;
    public StubTenantRepo(Tenant t) => _t = t;
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Tenant?>(id == _t.Id ? _t : null);
    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Tenant?> GetByCustomDomainOrNullAsync(string host, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Tenant>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddAsync(Tenant tenant, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => throw new NotImplementedException();
}
