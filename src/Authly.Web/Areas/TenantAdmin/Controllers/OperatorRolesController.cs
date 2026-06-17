using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Modules.Operators;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Management of the organization's operator roles and their permission mappings — the console-operator
/// counterpart of <see cref="RolesController"/> (which manages tenant end-user RBAC). Drives
/// <see cref="IOperatorRbacService"/>; system roles are protected from rename/delete.
/// </summary>
[Route("tenantadmin/operator-roles")]
public sealed class OperatorRolesController : TenantAdminControllerBase
{
    private readonly IOperatorRbacService _rbac;

    public OperatorRolesController(IOperatorRbacService rbac, ITenantContext tenant) : base(tenant)
        => _rbac = rbac;

    [RequireOperatorPermission("role.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Operator roles";
        // Ensure the org has its system operator roles even on the first visit.
        await _rbac.EnsureSystemRolesAsync(OrgId, ct);
        return View(await _rbac.ListRolesAsync(OrgId, ct));
    }

    [RequireOperatorPermission("role.read")]
    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New operator role";
        return View(new CreateRoleViewModel());
    }

    [RequireOperatorPermission("role.manage")]
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoleViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "New operator role";
        if (!ModelState.IsValid) return View(model);

        try
        {
            var role = await _rbac.CreateRoleAsync(OrgId,
                new CreateRoleRequest(model.Name, model.Description), CurrentAudit(), ct);
            TempData["Success"] = $"Role '{role.Name}' created. Assign permissions below.";
            return RedirectToAction(nameof(Details), new { id = role.Id });
        }
        catch (RoleNameAlreadyExistsException)
        {
            ModelState.AddModelError(nameof(model.Name), "A role with that name already exists.");
            return View(model);
        }
    }

    [RequireOperatorPermission("role.read")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var role = await _rbac.GetRoleAsync(OrgId, id, ct);
        if (role is null) return NotFound();

        ViewData["Title"] = role.Role.Name;
        ViewData["AllPermissions"] = await _rbac.ListPermissionsAsync(OrgId, ct);
        ViewData["AssignedPermissionIds"] = role.PermissionIds.ToHashSet();
        return View(role.Role);
    }

    [RequireOperatorPermission("role.manage")]
    [HttpPost("{id:guid}/permissions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPermissions(Guid id, Guid[] permissionIds, CancellationToken ct)
    {
        try
        {
            await _rbac.SetRolePermissionsAsync(OrgId, id, permissionIds ?? Array.Empty<Guid>(), CurrentAudit(), ct);
            TempData["Success"] = "Permissions updated.";
        }
        catch (RoleNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [RequireOperatorPermission("role.manage")]
    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _rbac.DeleteRoleAsync(OrgId, id, CurrentAudit(), ct);
            TempData["Success"] = "Role deleted.";
        }
        catch (SystemRoleProtectedException)
        {
            TempData["Error"] = "System roles cannot be deleted.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (RoleNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
