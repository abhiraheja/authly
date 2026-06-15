using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class RoleRepository : IRoleRepository
{
    private readonly AppDbContext _db;

    public RoleRepository(AppDbContext db) => _db = db;

    // --- Roles ---

    public async Task<IReadOnlyList<Role>> ListRolesAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Roles
            .Where(r => r.TenantId == tenantId)
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .ToListAsync(ct);

    public Task<Role?> GetRoleAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == id, ct);

    public Task<Role?> GetRoleByNameAsync(Guid tenantId, string name, CancellationToken ct = default)
        => _db.Roles.FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Name == name, ct);

    public Task<bool> AnyRolesAsync(Guid tenantId, CancellationToken ct = default)
        => _db.Roles.AnyAsync(r => r.TenantId == tenantId, ct);

    public async Task AddRoleAsync(Role role, CancellationToken ct = default)
    {
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateRoleAsync(Role role, CancellationToken ct = default)
    {
        _db.Roles.Update(role);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteRoleAsync(Role role, CancellationToken ct = default)
    {
        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
    }

    // --- Permissions ---

    public async Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Permissions
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Resource).ThenBy(p => p.Action)
            .ToListAsync(ct);

    public Task<Permission?> GetPermissionAsync(Guid tenantId, string resource, string action, CancellationToken ct = default)
        => _db.Permissions.FirstOrDefaultAsync(
            p => p.TenantId == tenantId && p.Resource == resource && p.Action == action, ct);

    public async Task AddPermissionAsync(Permission permission, CancellationToken ct = default)
    {
        _db.Permissions.Add(permission);
        await _db.SaveChangesAsync(ct);
    }

    // --- Role ↔ permission mapping ---

    public async Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid roleId, CancellationToken ct = default)
        => await _db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToListAsync(ct);

    public async Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default)
    {
        var existing = await _db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        _db.RolePermissions.RemoveRange(existing);
        foreach (var pid in permissionIds.Distinct())
            _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = pid });
        await _db.SaveChangesAsync(ct);
    }
}
