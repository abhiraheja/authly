using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Operators;

namespace Authly.Tests.Operators;

public class OperatorRbacServiceTests
{
    [Fact]
    public async Task EnsureSystemRoles_seeds_catalogue_and_roles_and_is_idempotent()
    {
        var roles = new InMemoryOperatorRoleRepo();
        var svc = new OperatorRbacService(roles, new InMemoryMemberRoleRepo());
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
