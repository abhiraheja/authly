using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class MemberRoleRepository : IMemberRoleRepository
{
    private readonly AppDbContext _db;

    public MemberRoleRepository(AppDbContext db) => _db = db;

    public async Task AssignAsync(MemberRole assignment, CancellationToken ct = default)
    {
        var exists = await _db.MemberRoles.AnyAsync(
            mr => mr.OrganizationMembershipId == assignment.OrganizationMembershipId && mr.OperatorRoleId == assignment.OperatorRoleId, ct);
        if (exists) return; // idempotent

        _db.MemberRoles.Add(assignment);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid organizationMembershipId, Guid operatorRoleId, CancellationToken ct = default)
        => await _db.MemberRoles
            .Where(mr => mr.OrganizationMembershipId == organizationMembershipId && mr.OperatorRoleId == operatorRoleId)
            .ExecuteDeleteAsync(ct);

    public async Task RemoveAllForMembershipAsync(Guid organizationMembershipId, CancellationToken ct = default)
        => await _db.MemberRoles
            .Where(mr => mr.OrganizationMembershipId == organizationMembershipId)
            .ExecuteDeleteAsync(ct);

    public async Task<IReadOnlyList<OperatorRole>> ListRolesForMembershipAsync(Guid organizationMembershipId, CancellationToken ct = default)
        => await _db.MemberRoles
            .Where(mr => mr.OrganizationMembershipId == organizationMembershipId)
            .Select(mr => mr.OperatorRole)
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Name)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid organizationMembershipId, CancellationToken ct = default)
        => await _db.MemberRoles
            .Where(mr => mr.OrganizationMembershipId == organizationMembershipId)
            .Select(mr => mr.OperatorRole.Name)
            .Distinct()
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid organizationMembershipId, CancellationToken ct = default)
    {
        // member_roles → operator_role_permissions → operator_permissions, flattened to distinct resource.action.
        var rows = await (
            from mr in _db.MemberRoles
            where mr.OrganizationMembershipId == organizationMembershipId
            join rp in _db.OperatorRolePermissions on mr.OperatorRoleId equals rp.OperatorRoleId
            join p in _db.OperatorPermissions on rp.OperatorPermissionId equals p.Id
            select new { p.Resource, p.Action })
            .Distinct()
            .ToListAsync(ct);

        return rows.Select(r => $"{r.Resource}.{r.Action}")
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<int> CountMembershipsWithRoleAsync(Guid organizationId, Guid operatorRoleId, CancellationToken ct = default)
        => await _db.MemberRoles
            .Where(mr => mr.OrganizationId == organizationId && mr.OperatorRoleId == operatorRoleId)
            .Select(mr => mr.OrganizationMembershipId)
            .Distinct()
            .CountAsync(ct);
}
