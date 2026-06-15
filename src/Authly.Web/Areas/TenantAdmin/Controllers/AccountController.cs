using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.TenantAdmins;
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
    private readonly ITenantAdminService _admins;
    private readonly ITenantContext _tenant;

    public AccountController(ITenantAdminService admins, ITenantContext tenant)
    {
        _admins = admins;
        _tenant = tenant;
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (!_tenant.HasTenant) return View("TenantRequired");
        ViewData["Layout"] = "_AuthLayout";
        return View(new TenantAdminLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(TenantAdminLoginViewModel model, CancellationToken ct)
    {
        if (!_tenant.HasTenant) return View("TenantRequired");
        ViewData["Layout"] = "_AuthLayout";
        if (!ModelState.IsValid) return View(model);

        var info = new RequestInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);

        var result = await _admins.SignInAsync(_tenant.TenantId!.Value, model.Email, model.Password, info, ct);
        if (!result.Succeeded || result.User is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid credentials, or this account is not a tenant administrator.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.User.Id.ToString()),
            new(ClaimTypes.Name, result.User.Email),
            new(TenantAdminClaims.TenantId, result.User.TenantId.ToString())
        };
        await HttpContext.SignInAsync(AuthSchemes.TenantAdmin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.TenantAdmin)),
            new AuthenticationProperties { IsPersistent = true });

        if (result.Bootstrapped)
            TempData["Success"] = "Welcome — you're now the administrator for this workspace.";

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
