using System.Security.Claims;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Accounts;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

[Area("TenantAdmin")]
[Route("tenantadmin/account")]
public sealed class AccountController : Controller
{
    private readonly IAccountService _accounts;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly ITenantRepository _tenants;
    private readonly Authly.Modules.Operators.IOperatorRbacService _operatorRbac;

    public AccountController(
        IAccountService accounts,
        IOrganizationMembershipRepository memberships,
        ITenantRepository tenants,
        Authly.Modules.Operators.IOperatorRbacService operatorRbac)
    {
        _accounts = accounts;
        _memberships = memberships;
        _tenants = tenants;
        _operatorRbac = operatorRbac;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        // Account login is tenant-agnostic — the account's org membership resolves the workspace.
        ViewData["Layout"] = "_AuthLayout";
        return View(new TenantAdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(TenantAdminLoginViewModel model, CancellationToken ct)
    {
        ViewData["Layout"] = "_AuthLayout";
        if (!ModelState.IsValid) return View(model);

        var account = await _accounts.ValidateCredentialsAsync(model.Email, model.Password, ct);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials.");
            return View(model);
        }

        // Resolve the active workspace: first Active membership → its first project.
        var memberships = await _memberships.ListByAccountAsync(account.Id, ct);
        var active = memberships.FirstOrDefault(m => m.Status == MembershipStatus.Active);
        if (active is null)
        {
            ModelState.AddModelError(string.Empty, "This account has no active workspace access.");
            return View(model);
        }

        var projects = await _tenants.ListByOrganizationAsync(active.OrganizationId, ct);
        var project = projects.FirstOrDefault();
        if (project is null)
        {
            ModelState.AddModelError(string.Empty, "This organization has no projects yet.");
            return View(model);
        }

        await _accounts.RecordLoginAsync(account.Id, ct);

        // Self-heal: ensure this org's system roles carry any permissions added in later releases
        // (e.g. policy.*/survey.*), so the owner can reach new console features without manual setup.
        await _operatorRbac.EnsureSystemRolesAsync(active.OrganizationId, ct);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Email),
            new(TenantAdminClaims.AccountId, account.Id.ToString()),
            new(TenantAdminClaims.OrgId, active.OrganizationId.ToString()),
            new(TenantAdminClaims.TenantId, project.Id.ToString())
        };
        await HttpContext.SignInAsync(AuthSchemes.TenantAdmin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.TenantAdmin)),
            new AuthenticationProperties { IsPersistent = true });

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Applications", new { area = "TenantAdmin" });
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicies.TenantAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.TenantAdmin);
        return RedirectToAction(nameof(Login));
    }
}
