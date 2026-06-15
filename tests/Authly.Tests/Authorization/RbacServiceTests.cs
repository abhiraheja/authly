using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Authorization;
using Authly.Modules.Common;

namespace Authly.Tests.Authorization;

public class RbacServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid OtherTenant = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task EnsureSystemRoles_seeds_roles_permissions_and_mappings()
    {
        var h = new Harness();
        await h.Service.EnsureSystemRolesAsync(Tenant);

        var roles = await h.Service.ListRolesAsync(Tenant);
        Assert.Equal(SystemRbac.RoleNames.Count, roles.Count);
        Assert.All(roles, r => Assert.True(r.IsSystem));
        Assert.Contains(roles, r => r.Name == SystemRbac.TenantAdmin);

        var permissions = await h.Service.ListPermissionsAsync(Tenant);
        Assert.Equal(SystemRbac.Permissions.Count, permissions.Count);

        // tenant_admin gets the full permission set.
        var admin = await h.Service.GetRoleAsync(Tenant, roles.First(r => r.Name == SystemRbac.TenantAdmin).Id);
        Assert.NotNull(admin);
        Assert.Equal(SystemRbac.Permissions.Count, admin!.PermissionIds.Count);

        // tenant_viewer is read-only.
        var viewer = roles.First(r => r.Name == SystemRbac.TenantViewer);
        var viewerPerms = await ResolvePermissionNames(h, viewer.Id);
        Assert.All(viewerPerms, p => Assert.EndsWith(".read", p));
    }

    [Fact]
    public async Task EnsureSystemRoles_is_idempotent_and_non_destructive()
    {
        var h = new Harness();
        await h.Service.EnsureSystemRolesAsync(Tenant);

        var member = (await h.Service.ListRolesAsync(Tenant)).First(r => r.Name == SystemRbac.TenantMember);
        // Admin customizes the system role: strip it down to nothing.
        await h.Service.SetRolePermissionsAsync(Tenant, member.Id, Array.Empty<Guid>(), AuditContext.System);

        // Re-seeding must not restore the defaults onto an existing role.
        await h.Service.EnsureSystemRolesAsync(Tenant);

        var after = await h.Service.GetRoleAsync(Tenant, member.Id);
        Assert.Empty(after!.PermissionIds);
        Assert.Equal(SystemRbac.RoleNames.Count, (await h.Service.ListRolesAsync(Tenant)).Count); // no duplicates
    }

    [Fact]
    public async Task CreateRole_rejects_duplicate_name()
    {
        var h = new Harness();
        await h.Service.CreateRoleAsync(Tenant, new CreateRoleRequest("billing", null), AuditContext.System);
        await Assert.ThrowsAsync<RoleNameAlreadyExistsException>(() =>
            h.Service.CreateRoleAsync(Tenant, new CreateRoleRequest("billing", null), AuditContext.System));
    }

    [Fact]
    public async Task DeleteRole_blocks_system_roles()
    {
        var h = new Harness();
        await h.Service.EnsureSystemRolesAsync(Tenant);
        var admin = (await h.Service.ListRolesAsync(Tenant)).First(r => r.Name == SystemRbac.TenantAdmin);
        await Assert.ThrowsAsync<SystemRoleProtectedException>(() =>
            h.Service.DeleteRoleAsync(Tenant, admin.Id, AuditContext.System));
    }

    [Fact]
    public async Task SetRolePermissions_ignores_ids_from_another_tenant()
    {
        var h = new Harness();
        await h.Service.EnsureSystemRolesAsync(Tenant);
        await h.Service.EnsureSystemRolesAsync(OtherTenant);

        var role = await h.Service.CreateRoleAsync(Tenant, new CreateRoleRequest("custom", null), AuditContext.System);
        var foreignPermId = (await h.Service.ListPermissionsAsync(OtherTenant)).First().Id;
        var ownPermId = (await h.Service.ListPermissionsAsync(Tenant)).First().Id;

        await h.Service.SetRolePermissionsAsync(Tenant, role.Id, new[] { ownPermId, foreignPermId }, AuditContext.System);

        var saved = await h.Service.GetRoleAsync(Tenant, role.Id);
        Assert.Equal(new[] { ownPermId }, saved!.PermissionIds);
    }

    [Fact]
    public async Task AssignRole_grants_user_roles_and_permissions_for_token()
    {
        var h = new Harness();
        await h.Service.EnsureSystemRolesAsync(Tenant);
        var viewer = (await h.Service.ListRolesAsync(Tenant)).First(r => r.Name == SystemRbac.TenantViewer);

        await h.Service.AssignRoleAsync(Tenant, UserId, viewer.Id, AuditContext.System);

        var auth = await h.Service.GetUserAuthorizationAsync(Tenant, UserId);
        Assert.Contains(SystemRbac.TenantViewer, auth.Roles);
        Assert.Contains("user.read", auth.Permissions);
        Assert.DoesNotContain("user.delete", auth.Permissions);
        Assert.Contains("user.role_assigned", h.Audit.Events);
    }

    [Fact]
    public async Task RemoveRole_revokes_permissions()
    {
        var h = new Harness();
        await h.Service.EnsureSystemRolesAsync(Tenant);
        var viewer = (await h.Service.ListRolesAsync(Tenant)).First(r => r.Name == SystemRbac.TenantViewer);
        await h.Service.AssignRoleAsync(Tenant, UserId, viewer.Id, AuditContext.System);

        await h.Service.RemoveRoleAsync(Tenant, UserId, viewer.Id, AuditContext.System);

        var auth = await h.Service.GetUserAuthorizationAsync(Tenant, UserId);
        Assert.Empty(auth.Roles);
        Assert.Empty(auth.Permissions);
    }

    private static async Task<IReadOnlyList<string>> ResolvePermissionNames(Harness h, Guid roleId)
    {
        var ids = (await h.Service.GetRoleAsync(Tenant, roleId))!.PermissionIds.ToHashSet();
        return (await h.Service.ListPermissionsAsync(Tenant)).Where(p => ids.Contains(p.Id)).Select(p => p.Name).ToList();
    }

    private sealed class Harness
    {
        public readonly FakeRoleRepository Roles = new();
        public readonly FakeUserRoleRepository UserRoles;
        public readonly RecordingAuditLogger Audit = new();
        public readonly RbacService Service;

        public Harness()
        {
            UserRoles = new FakeUserRoleRepository(Roles);
            Service = new RbacService(Roles, UserRoles, Audit);
        }
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        public readonly List<Role> Roles = new();
        public readonly List<Permission> Permissions = new();
        public readonly List<RolePermission> Map = new();

        public Task<IReadOnlyList<Role>> ListRolesAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Role>>(Roles.Where(r => r.TenantId == tenantId).ToList());
        public Task<Role?> GetRoleAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Roles.FirstOrDefault(r => r.TenantId == tenantId && r.Id == id));
        public Task<Role?> GetRoleByNameAsync(Guid tenantId, string name, CancellationToken ct = default)
            => Task.FromResult(Roles.FirstOrDefault(r => r.TenantId == tenantId && r.Name == name));
        public Task<bool> AnyRolesAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult(Roles.Any(r => r.TenantId == tenantId));
        public Task AddRoleAsync(Role role, CancellationToken ct = default)
        {
            if (role.Id == Guid.Empty) role.Id = Guid.NewGuid();
            Roles.Add(role);
            return Task.CompletedTask;
        }
        public Task UpdateRoleAsync(Role role, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteRoleAsync(Role role, CancellationToken ct = default)
        {
            Roles.Remove(role);
            Map.RemoveAll(m => m.RoleId == role.Id);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Permission>>(Permissions.Where(p => p.TenantId == tenantId).ToList());
        public Task<Permission?> GetPermissionAsync(Guid tenantId, string resource, string action, CancellationToken ct = default)
            => Task.FromResult(Permissions.FirstOrDefault(p => p.TenantId == tenantId && p.Resource == resource && p.Action == action));
        public Task AddPermissionAsync(Permission permission, CancellationToken ct = default)
        {
            if (permission.Id == Guid.Empty) permission.Id = Guid.NewGuid();
            Permissions.Add(permission);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid roleId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(Map.Where(m => m.RoleId == roleId).Select(m => m.PermissionId).ToList());
        public Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default)
        {
            Map.RemoveAll(m => m.RoleId == roleId);
            foreach (var pid in permissionIds.Distinct())
                Map.Add(new RolePermission { RoleId = roleId, PermissionId = pid });
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUserRoleRepository : IUserRoleRepository
    {
        private readonly FakeRoleRepository _roles;
        public readonly List<UserRole> Items = new();

        public FakeUserRoleRepository(FakeRoleRepository roles) => _roles = roles;

        public Task AssignAsync(UserRole assignment, CancellationToken ct = default)
        {
            if (!Items.Any(u => u.UserId == assignment.UserId && u.RoleId == assignment.RoleId))
                Items.Add(assignment);
            return Task.CompletedTask;
        }
        public Task RemoveAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken ct = default)
        {
            Items.RemoveAll(u => u.TenantId == tenantId && u.UserId == userId && u.RoleId == roleId);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<Role>> ListRolesForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Role>>(
                Items.Where(u => u.TenantId == tenantId && u.UserId == userId)
                     .Select(u => _roles.Roles.First(r => r.Id == u.RoleId)).ToList());
        public Task<IReadOnlyList<Guid>> ListUserIdsForRoleAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(
                Items.Where(u => u.TenantId == tenantId && u.RoleId == roleId).Select(u => u.UserId).ToList());
        public Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(
                Items.Where(u => u.TenantId == tenantId && u.UserId == userId)
                     .Select(u => _roles.Roles.First(r => r.Id == u.RoleId).Name).Distinct().ToList());
        public Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        {
            var roleIds = Items.Where(u => u.TenantId == tenantId && u.UserId == userId).Select(u => u.RoleId).ToHashSet();
            var permIds = _roles.Map.Where(m => roleIds.Contains(m.RoleId)).Select(m => m.PermissionId).ToHashSet();
            var names = _roles.Permissions.Where(p => permIds.Contains(p.Id)).Select(p => p.Name).Distinct().ToList();
            return Task.FromResult<IReadOnlyList<string>>(names);
        }
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public readonly List<string> Events = new();
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null,
            string? resourceType = null, Guid? resourceId = null, string result = "success",
            object? metadata = null, CancellationToken ct = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }
}
