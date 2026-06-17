using Authly.Core.Interfaces;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Organization-level settings for the active org: rename and (guarded) delete. Gated on
/// <c>org.manage</c> (held only by org_owner among the system roles).</summary>
[Route("tenantadmin/organization")]
public sealed class OrganizationController : TenantAdminControllerBase
{
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;

    public OrganizationController(
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        ITenantContext tenant) : base(tenant)
    {
        _organizations = organizations;
        _tenants = tenants;
    }

    [RequireOperatorPermission("org.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Organization";
        var org = await _organizations.GetByIdAsync(OrgId, ct);
        if (org is null) return NotFound();

        var projects = await _tenants.ListByOrganizationAsync(OrgId, ct);
        return View(new OrganizationSettingsViewModel
        {
            Name = org.Name,
            Slug = org.Slug,
            ProjectCount = projects.Count
        });
    }

    [RequireOperatorPermission("org.manage")]
    [HttpPost("rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(OrganizationSettingsViewModel model, CancellationToken ct)
    {
        var org = await _organizations.GetByIdAsync(OrgId, ct);
        if (org is null) return NotFound();

        if (!ModelState.IsValid)
        {
            model.Slug = org.Slug;
            model.ProjectCount = (await _tenants.ListByOrganizationAsync(OrgId, ct)).Count;
            return View(nameof(Index), model);
        }

        org.Name = model.Name.Trim();
        org.UpdatedAt = DateTimeOffset.UtcNow;
        await _organizations.UpdateAsync(org, ct);

        TempData["Success"] = "Organization renamed.";
        return RedirectToAction(nameof(Index));
    }

    [RequireOperatorPermission("org.manage")]
    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string confirmName, CancellationToken ct)
    {
        var org = await _organizations.GetByIdAsync(OrgId, ct);
        if (org is null) return NotFound();

        // Projects use a Restrict FK to the org, so an org with projects can't be deleted —
        // surface that as a clear guard rather than a database error.
        var projects = await _tenants.ListByOrganizationAsync(OrgId, ct);
        if (projects.Count > 0)
        {
            TempData["Error"] = "Delete or move every project before deleting the organization.";
            return RedirectToAction(nameof(Index));
        }

        if (!string.Equals(confirmName?.Trim(), org.Name, StringComparison.Ordinal))
        {
            TempData["Error"] = "Type the organization name exactly to confirm deletion.";
            return RedirectToAction(nameof(Index));
        }

        await _organizations.DeleteAsync(org, ct);

        // The active workspace is gone — sign the operator out.
        await HttpContext.SignOutAsync(AuthSchemes.TenantAdmin);
        return RedirectToAction("Login", "Account", new { area = "TenantAdmin" });
    }
}
