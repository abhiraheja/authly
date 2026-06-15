using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Web.Areas.TenantAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin view of users and their role assignments.</summary>
[Route("tenantadmin/users")]
public sealed class UsersController : TenantAdminControllerBase
{
    private readonly IUserRepository _users;
    private readonly IRbacService _rbac;

    public UsersController(IUserRepository users, IRbacService rbac, ITenantContext tenant) : base(tenant)
    {
        _users = users;
        _rbac = rbac;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Users";
        return View(await _users.ListByTenantAsync(TenantId, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(TenantId, id, ct);
        if (user is null) return NotFound();

        var assigned = await _rbac.ListUserRolesAsync(TenantId, id, ct);
        var all = await _rbac.ListRolesAsync(TenantId, ct);
        var assignedIds = assigned.Select(r => r.Id).ToHashSet();

        ViewData["Title"] = user.Email;
        return View(new UserRolesViewModel
        {
            UserId = user.Id,
            Email = user.Email,
            AssignedRoles = assigned,
            AvailableRoles = all.Where(r => !assignedIds.Contains(r.Id)).ToList()
        });
    }

    [HttpPost("{id:guid}/roles/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid id, Guid roleId, CancellationToken ct)
    {
        try
        {
            await _rbac.AssignRoleAsync(TenantId, id, roleId, CurrentAudit(), ct);
            TempData["Success"] = "Role assigned.";
        }
        catch (RoleNotFoundException)
        {
            TempData["Error"] = "That role no longer exists.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/roles/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id, Guid roleId, CancellationToken ct)
    {
        try
        {
            await _rbac.RemoveRoleAsync(TenantId, id, roleId, CurrentAudit(), ct);
            TempData["Success"] = "Role removed.";
        }
        catch (RoleNotFoundException)
        {
            TempData["Error"] = "That role no longer exists.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
