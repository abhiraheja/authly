using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Modules.Members;
using Authly.Modules.Operators;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Console employees (operators) and their operator-role assignments for the active organization.
/// Distinct from end-user management (<see cref="UsersController"/>): this operates on the global
/// Account/Organization layer (doc 06 §7–§8).
/// </summary>
[Route("tenantadmin/members")]
public sealed class MembersController : TenantAdminControllerBase
{
    private readonly IMemberDirectoryService _members;
    private readonly IInvitationService _invites;
    private readonly IOperatorRbacService _rbac;

    public MembersController(
        IMemberDirectoryService members,
        IInvitationService invites,
        IOperatorRbacService rbac,
        ITenantContext tenant) : base(tenant)
    {
        _members = members;
        _invites = invites;
        _rbac = rbac;
    }

    [RequireOperatorPermission("member.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Members";
        return View(await _members.ListMembersAsync(OrgId, ct));
    }

    [RequireOperatorPermission("member.invite")]
    [HttpGet("invite")]
    public async Task<IActionResult> Invite(CancellationToken ct)
    {
        ViewData["Title"] = "Invite member";
        await _rbac.EnsureSystemRolesAsync(OrgId, ct);
        return View(new InviteMemberViewModel { AvailableRoles = await _rbac.ListRolesAsync(OrgId, ct) });
    }

    [RequireOperatorPermission("member.invite")]
    [HttpPost("invite")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Invite(InviteMemberViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Invite member";
        if (!ModelState.IsValid)
        {
            model.AvailableRoles = await _rbac.ListRolesAsync(OrgId, ct);
            return View(model);
        }

        try
        {
            // The accept email is sent through the active project's messaging provider.
            await _invites.InviteAsync(OrgId, TenantId, model.Email, model.RoleIds, CurrentAudit(), ct);
            TempData["Success"] = $"Invite sent to {model.Email.Trim()}.";
            return RedirectToAction(nameof(Index));
        }
        catch (InviteAccountException ex)
        {
            ModelState.AddModelError(nameof(model.Email), ex.Message);
            model.AvailableRoles = await _rbac.ListRolesAsync(OrgId, ct);
            return View(model);
        }
    }

    [RequireOperatorPermission("member.read")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var member = await _members.GetMemberAsync(OrgId, id, ct);
        if (member is null) return NotFound();

        var assigned = await _rbac.ListMemberRolesAsync(id, ct);
        var all = await _rbac.ListRolesAsync(OrgId, ct);
        var assignedIds = assigned.Select(r => r.Id).ToHashSet();

        ViewData["Title"] = member.Email;
        return View(new MemberRolesViewModel
        {
            MembershipId = member.MembershipId,
            Email = member.Email,
            Name = member.Name,
            IsOwner = member.IsOwner,
            AssignedRoles = assigned,
            AvailableRoles = all.Where(r => !assignedIds.Contains(r.Id)).ToList()
        });
    }

    [RequireOperatorPermission("member.manage")]
    [HttpPost("{id:guid}/roles/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid id, Guid roleId, CancellationToken ct)
    {
        try
        {
            await _rbac.AssignRoleToMemberAsync(OrgId, id, roleId, CurrentAudit(), ct);
            TempData["Success"] = "Role assigned.";
        }
        catch (RoleNotFoundException)
        {
            TempData["Error"] = "That role no longer exists.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [RequireOperatorPermission("member.manage")]
    [HttpPost("{id:guid}/roles/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id, Guid roleId, CancellationToken ct)
    {
        try
        {
            await _rbac.RemoveRoleFromMemberAsync(OrgId, id, roleId, CurrentAudit(), ct);
            TempData["Success"] = "Role removed.";
        }
        catch (RoleNotFoundException)
        {
            TempData["Error"] = "That role no longer exists.";
        }
        catch (LastOwnerProtectedException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [RequireOperatorPermission("member.manage")]
    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _members.RemoveMemberAsync(OrgId, id, CurrentAudit(), ct);
            TempData["Success"] = "Member removed.";
            return RedirectToAction(nameof(Index));
        }
        catch (MemberNotFoundException)
        {
            return NotFound();
        }
        catch (LastOwnerProtectedException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
