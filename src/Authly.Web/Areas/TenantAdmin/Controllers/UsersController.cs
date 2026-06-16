using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Modules.Common;
using Authly.Modules.Users;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin view of users and their role assignments.</summary>
[Route("tenantadmin/users")]
public sealed class UsersController : TenantAdminControllerBase
{
    private readonly IUserRepository _users;
    private readonly IRbacService _rbac;
    private readonly IImpersonationService _impersonation;
    private readonly IUserImportService _import;

    public UsersController(IUserRepository users, IRbacService rbac, IImpersonationService impersonation,
        IUserImportService import, ITenantContext tenant) : base(tenant)
    {
        _users = users;
        _rbac = rbac;
        _impersonation = impersonation;
        _import = import;
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

    /// <summary>
    /// Start impersonating a user: mint a session for them and issue the end-user cookie carrying
    /// the impersonator's identity (so the portal shows a banner and the act is reversible). The
    /// admin's own TenantAdmin cookie is untouched, so "stop" returns them straight here.
    /// </summary>
    [HttpPost("{id:guid}/impersonate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Impersonate(Guid id, CancellationToken ct)
    {
        var info = new RequestInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
        try
        {
            var result = await _impersonation.StartAsync(TenantId, CurrentUserId, id, info, CurrentAudit(), ct);
            await UserSignIn.SignInAsync(HttpContext, result.User.Id, result.User.Email, result.User.TenantId,
                result.Session.Id, result.User.EmailVerified,
                impersonatorId: CurrentUserId, impersonatorEmail: User.Identity?.Name);
            return Redirect("/portal/profile");
        }
        catch (ImpersonationNotAllowedException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [HttpGet("import")]
    public IActionResult Import()
    {
        ViewData["Title"] = "Import users";
        return View();
    }

    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(ImportSource source, string? json, CancellationToken ct)
    {
        ViewData["Title"] = "Import users";
        if (string.IsNullOrWhiteSpace(json))
        {
            TempData["Error"] = "Paste the export JSON to import.";
            return View();
        }

        var result = await _import.ImportAsync(TenantId, source, json, CurrentAudit(), ct);
        ViewBag.Result = result;
        TempData["Success"] = $"Imported {result.Created} user(s); {result.Skipped} skipped.";
        return View();
    }
}
