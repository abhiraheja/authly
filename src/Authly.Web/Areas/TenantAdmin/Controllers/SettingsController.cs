using System.Security.Claims;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Tenants;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Settings for the active project (tenant): shows identity + the owner "Delete project" action,
/// gated on <c>project.delete</c>. Replaces the deleted SuperAdmin tenant-delete surface (doc 04 —
/// tenant delete folds into project settings).
/// </summary>
[Route("tenantadmin/settings")]
public sealed class SettingsController : TenantAdminControllerBase
{
    private readonly ITenantService _tenants;
    private readonly ITenantRepository _tenantRepo;

    public SettingsController(ITenantService tenants, ITenantRepository tenantRepo, ITenantContext tenant) : base(tenant)
    {
        _tenants = tenants;
        _tenantRepo = tenantRepo;
    }

    [RequireOperatorPermission("project.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Project settings";
        var project = await _tenants.GetAsync(TenantId, ct);
        if (project is null) return NotFound();

        return View(new ProjectSettingsViewModel { Name = project.Name, Slug = project.Slug });
    }

    [RequireOperatorPermission("project.delete")]
    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string confirmSlug, CancellationToken ct)
    {
        var project = await _tenants.GetAsync(TenantId, ct);
        if (project is null) return NotFound();

        if (!string.Equals(confirmSlug?.Trim(), project.Slug, StringComparison.Ordinal))
        {
            TempData["Error"] = "Type the project slug exactly to confirm deletion.";
            return RedirectToAction(nameof(Index));
        }

        await _tenants.DeleteAsync(TenantId, CurrentAudit(), ct);

        // The active project is gone — switch to another active project in the org, or sign out.
        var next = (await _tenantRepo.ListByOrganizationAsync(OrgId, ct))
            .FirstOrDefault(t => t.Id != TenantId && t.Status != TenantStatus.Deleted);
        if (next is null)
        {
            await HttpContext.SignOutAsync(AuthSchemes.TenantAdmin);
            return RedirectToAction("Login", "Account", new { area = "TenantAdmin" });
        }

        await ReIssueAsync(OrgId, next.Id);
        TempData["Success"] = $"Project '{project.Name}' deleted.";
        return RedirectToAction("Index", "Applications", new { area = "TenantAdmin" });
    }

    // Re-issue the tenant-admin cookie with a new active project (same shape as WorkspaceController).
    private async Task ReIssueAsync(Guid orgId, Guid projectId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, CurrentAccountId.ToString()),
            new(ClaimTypes.Name, User.Identity?.Name ?? string.Empty),
            new(TenantAdminClaims.AccountId, CurrentAccountId.ToString()),
            new(TenantAdminClaims.OrgId, orgId.ToString()),
            new(TenantAdminClaims.TenantId, projectId.ToString())
        };
        await HttpContext.SignInAsync(AuthSchemes.TenantAdmin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.TenantAdmin)),
            new AuthenticationProperties { IsPersistent = true });
    }
}

public sealed class ProjectSettingsViewModel
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
}
