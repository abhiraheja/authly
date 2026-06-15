using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Web.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers.Api;

/// <summary>Management API: roles + role-permission mapping (§6).</summary>
[Route("api/v1/roles")]
public sealed class RolesController : ApiControllerBase
{
    private readonly IRbacService _rbac;

    public RolesController(IRbacService rbac, ITenantContext tenant) : base(tenant) => _rbac = rbac;

    [HttpGet("")]
    [RequirePermission("role.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await _rbac.EnsureSystemRolesAsync(TenantId, ct);
        var roles = await _rbac.ListRolesAsync(TenantId, ct);
        return Ok(roles.Select(RoleResponse.From).ToList());
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("role.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var role = await _rbac.GetRoleAsync(TenantId, id, ct);
        return role is null ? NotFoundError("Role not found.") : Ok(RoleResponse.From(role.Role));
    }

    [HttpPost("")]
    [RequirePermission("role.write")]
    public async Task<IActionResult> Create([FromBody] CreateRoleDto dto, CancellationToken ct)
    {
        var role = await _rbac.CreateRoleAsync(TenantId, new CreateRoleRequest(dto.Name, dto.Description), ApiAudit(), ct);
        return CreatedAtAction(nameof(Get), new { id = role.Id }, RoleResponse.From(role));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("role.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _rbac.DeleteRoleAsync(TenantId, id, ApiAudit(), ct);
        return NoContent();
    }

    // --- Role permissions ---

    [HttpGet("{id:guid}/permissions")]
    [RequirePermission("role.read")]
    public async Task<IActionResult> GetPermissions(Guid id, CancellationToken ct)
    {
        var role = await _rbac.GetRoleAsync(TenantId, id, ct);
        if (role is null) return NotFoundError("Role not found.");

        var ids = role.PermissionIds.ToHashSet();
        var permissions = (await _rbac.ListPermissionsAsync(TenantId, ct)).Where(p => ids.Contains(p.Id));
        return Ok(permissions.Select(PermissionResponse.From).ToList());
    }

    [HttpPost("{id:guid}/permissions")]
    [RequirePermission("role.write")]
    public async Task<IActionResult> SetPermissions(Guid id, [FromBody] SetRolePermissionsDto dto, CancellationToken ct)
    {
        await _rbac.SetRolePermissionsAsync(TenantId, id, dto.PermissionIds, ApiAudit(), ct);
        return NoContent();
    }
}
