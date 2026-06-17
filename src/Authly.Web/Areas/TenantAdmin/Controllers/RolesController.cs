using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin management of roles and their permission mappings.</summary>
[Route("tenantadmin/roles")]
public sealed class RolesController : TenantAdminControllerBase
{
    private readonly IRbacService _rbac;

    public RolesController(IRbacService rbac, ITenantContext tenant) : base(tenant)
        => _rbac = rbac;

    [RequireOperatorPermission("role.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Roles & permissions";
        // Ensure the workspace has its system roles even on the first visit.
        await _rbac.EnsureSystemRolesAsync(TenantId, ct);
        return View(await _rbac.ListRolesAsync(TenantId, ct));
    }

    [RequireOperatorPermission("role.read")]
    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New role";
        return View(new CreateRoleViewModel());
    }

    [RequireOperatorPermission("role.manage")]
    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoleViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "New role";
        if (!ModelState.IsValid) return View(model);

        try
        {
            var role = await _rbac.CreateRoleAsync(TenantId,
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
        var role = await _rbac.GetRoleAsync(TenantId, id, ct);
        if (role is null) return NotFound();

        ViewData["Title"] = role.Role.Name;
        ViewData["AllPermissions"] = await _rbac.ListPermissionsAsync(TenantId, ct);
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
            await _rbac.SetRolePermissionsAsync(TenantId, id, permissionIds ?? Array.Empty<Guid>(), CurrentAudit(), ct);
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
            await _rbac.DeleteRoleAsync(TenantId, id, CurrentAudit(), ct);
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
