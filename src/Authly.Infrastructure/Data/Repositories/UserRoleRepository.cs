using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class UserRoleRepository : IUserRoleRepository
{
    private readonly AppDbContext _db;

    public UserRoleRepository(AppDbContext db) => _db = db;

    public async Task AssignAsync(UserRole assignment, CancellationToken ct = default)
    {
        var exists = await _db.UserRoles.AnyAsync(
            ur => ur.UserId == assignment.UserId && ur.RoleId == assignment.RoleId, ct);
        if (exists) return; // idempotent: assigning an already-held role is a no-op

        _db.UserRoles.Add(assignment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid tenantId, Guid userId, Guid roleId, CancellationToken ct = default)
        => await _db.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.UserId == userId && ur.RoleId == roleId)
            .ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<Role>> ListRolesForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.UserId == userId)
            .Select(ur => ur.Role)
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Guid>> ListUserIdsForRoleAsync(Guid tenantId, Guid roleId, CancellationToken ct = default)
        => await _db.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.UserRoles
            .Where(ur => ur.TenantId == tenantId && ur.UserId == userId)
            .Select(ur => ur.Role.Name)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        // user_roles → role_permissions → permissions, flattened to distinct resource.action.
        var rows = await (
            from ur in _db.UserRoles
            where ur.TenantId == tenantId && ur.UserId == userId
            join rp in _db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in _db.Permissions on rp.PermissionId equals p.Id
            select new { p.Resource, p.Action })
            .Distinct()
            .ToListAsync(ct);

        return rows.Select(r => $"{r.Resource}.{r.Action}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }
}
