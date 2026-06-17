using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Authly.Core.Authorization;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Operators;
using Authly.Modules.Provisioning;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Two-level workspace switching (org → project) and self-serve "New project" (doc 06 §6).
/// Deliberately NOT derived from <see cref="TenantAdminControllerBase"/>: switching changes the
/// active org/project, so it runs its own membership checks rather than the base guard (which
/// validates the <em>current</em> cookie). Each successful switch re-issues the cookie.
/// </summary>
[Area("TenantAdmin")]
[Route("tenantadmin/workspace")]
[Authorize(Policy = AuthPolicies.TenantAdmin)]
public sealed class WorkspaceController : Controller
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly ITenantRepository _tenants;
    private readonly IConsoleAccessService _access;
    private readonly IConsoleProvisioningService _provisioning;

    public WorkspaceController(
        IOrganizationMembershipRepository memberships,
        ITenantRepository tenants,
        IConsoleAccessService access,
        IConsoleProvisioningService provisioning)
    {
        _memberships = memberships;
        _tenants = tenants;
        _access = access;
        _provisioning = provisioning;
    }

    private Guid AccountId => Guid.Parse(User.FindFirstValue(TenantAdminClaims.AccountId) ?? User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private Guid CurrentOrgId => Guid.Parse(User.FindFirstValue(TenantAdminClaims.OrgId)!);
    private Guid CurrentProjectId => Guid.Parse(User.FindFirstValue(TenantAdminClaims.TenantId)!);

    [HttpPost("switch-org")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchOrg(Guid organizationId, CancellationToken ct)
    {
        // Must be an Active member of the target org.
        var membership = await _memberships.GetAsync(AccountId, organizationId, ct);
        if (membership is null || membership.Status != MembershipStatus.Active)
            return Forbid(AuthSchemes.TenantAdmin);

        var project = (await _tenants.ListByOrganizationAsync(organizationId, ct)).FirstOrDefault();
        if (project is null)
        {
            TempData["Error"] = "That organization has no projects yet.";
            return Back();
        }

        await ReIssueAsync(organizationId, project.Id);
        return RedirectToApplications();
    }

    [HttpPost("switch-project")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchProject(Guid projectId, CancellationToken ct)
    {
        var project = await _tenants.GetByIdAsync(projectId, ct);
        if (project is null) return NotFound();

        // Must be an Active member of the project's org.
        var membership = await _memberships.GetAsync(AccountId, project.OrganizationId, ct);
        if (membership is null || membership.Status != MembershipStatus.Active)
            return Forbid(AuthSchemes.TenantAdmin);

        await ReIssueAsync(project.OrganizationId, project.Id);
        return RedirectToApplications();
    }

    [HttpGet("new-project")]
    public async Task<IActionResult> NewProject(CancellationToken ct)
    {
        if (!await CanCreateAsync(ct)) return Forbid(AuthSchemes.TenantAdmin);
        return View(new NewProjectViewModel());
    }

    [HttpPost("new-project")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewProject(NewProjectViewModel model, CancellationToken ct)
    {
        if (!await CanCreateAsync(ct)) return Forbid(AuthSchemes.TenantAdmin);
        if (!ModelState.IsValid) return View(model);

        var actor = new AuditContext(AccountId, "account",
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);

        try
        {
            var project = await _provisioning.CreateProjectAsync(CurrentOrgId, model.Name.Trim(), actor, ct);
            await ReIssueAsync(CurrentOrgId, project.Id);   // auto-switch into the new project
            TempData["Success"] = $"Project '{project.Name}' created.";
            return RedirectToApplications();
        }
        catch (ProjectProvisioningException ex)
        {
            ModelState.AddModelError(nameof(model.Name), ex.Message);
            return View(model);
        }
    }

    // The active account must hold project.create in the current workspace.
    private async Task<bool> CanCreateAsync(CancellationToken ct)
    {
        var access = await _access.ResolveAsync(AccountId, CurrentOrgId, CurrentProjectId, ct);
        return access is not null && PermissionEvaluator.Satisfies(access.Permissions, "project.create");
    }

    // Re-issue the tenant-admin cookie with the new active org/project (single cookie, one source).
    private async Task ReIssueAsync(Guid orgId, Guid projectId)
    {
        var email = User.Identity?.Name ?? string.Empty;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, AccountId.ToString()),
            new(ClaimTypes.Name, email),
            new(TenantAdminClaims.AccountId, AccountId.ToString()),
            new(TenantAdminClaims.OrgId, orgId.ToString()),
            new(TenantAdminClaims.TenantId, projectId.ToString())
        };
        await HttpContext.SignInAsync(AuthSchemes.TenantAdmin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.TenantAdmin)),
            new AuthenticationProperties { IsPersistent = true });
    }

    private IActionResult RedirectToApplications() => RedirectToAction("Index", "Applications", new { area = "TenantAdmin" });

    private IActionResult Back()
    {
        var referer = Request.Headers.Referer.ToString();
        return !string.IsNullOrEmpty(referer) && Url.IsLocalUrl(referer) ? Redirect(referer) : RedirectToApplications();
    }
}

public sealed class NewProjectViewModel
{
    [Required, StringLength(80, MinimumLength = 2)]
    [Display(Name = "Project name")]
    public string Name { get; set; } = string.Empty;
}
