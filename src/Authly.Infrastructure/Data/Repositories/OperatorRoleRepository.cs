using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class OperatorRoleRepository : IOperatorRoleRepository
{
    private readonly AppDbContext _db;

    public OperatorRoleRepository(AppDbContext db) => _db = db;

    // --- Roles ---

    public async Task<IReadOnlyList<OperatorRole>> ListRolesAsync(Guid organizationId, CancellationToken ct = default)
        => await _db.OperatorRoles
            .Where(r => r.OrganizationId == organizationId)
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .ToListAsync(ct);

    public Task<OperatorRole?> GetRoleAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        => _db.OperatorRoles.FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Id == id, ct);

    public Task<OperatorRole?> GetRoleByNameAsync(Guid organizationId, string name, CancellationToken ct = default)
        => _db.OperatorRoles.FirstOrDefaultAsync(r => r.OrganizationId == organizationId && r.Name == name, ct);

    public async Task AddRoleAsync(OperatorRole role, CancellationToken ct = default)
    {
        _db.OperatorRoles.Add(role);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateRoleAsync(OperatorRole role, CancellationToken ct = default)
    {
        _db.OperatorRoles.Update(role);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteRoleAsync(OperatorRole role, CancellationToken ct = default)
    {
        _db.OperatorRoles.Remove(role);
        await _db.SaveChangesAsync(ct);
    }

    // --- Permissions ---

    public async Task<IReadOnlyList<OperatorPermission>> ListPermissionsAsync(Guid organizationId, CancellationToken ct = default)
        => await _db.OperatorPermissions
            .Where(p => p.OrganizationId == organizationId)
            .OrderBy(p => p.Resource).ThenBy(p => p.Action)
            .ToListAsync(ct);

    public async Task AddPermissionAsync(OperatorPermission permission, CancellationToken ct = default)
    {
        _db.OperatorPermissions.Add(permission);
        await _db.SaveChangesAsync(ct);
    }

    // --- Role ↔ permission mapping ---

    public async Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid roleId, CancellationToken ct = default)
        => await _db.OperatorRolePermissions
            .Where(rp => rp.OperatorRoleId == roleId)
            .Select(rp => rp.OperatorPermissionId)
            .ToListAsync(ct);

    public async Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default)
    {
        var existing = await _db.OperatorRolePermissions.Where(rp => rp.OperatorRoleId == roleId).ToListAsync(ct);
        _db.OperatorRolePermissions.RemoveRange(existing);
        foreach (var pid in permissionIds.Distinct())
            _db.OperatorRolePermissions.Add(new OperatorRolePermission { OperatorRoleId = roleId, OperatorPermissionId = pid });
        await _db.SaveChangesAsync(ct);
    }
}
