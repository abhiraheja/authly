using System.Security.Claims;
using Authly.Modules.SuperAdmins;
using Authly.Web.Areas.SuperAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.SuperAdmin.Controllers;

[Area("SuperAdmin")]
[Route("superadmin/account")]
public sealed class AccountController : Controller
{
    private readonly ISuperAdminService _superAdmins;

    public AccountController(ISuperAdminService superAdmins) => _superAdmins = superAdmins;

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["Layout"] = "_AuthLayout";
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken ct)
    {
        ViewData["Layout"] = "_AuthLayout";
        if (!ModelState.IsValid) return View(model);

        var admin = await _superAdmins.ValidateCredentialsAsync(model.Email, model.Password, ct);
        if (admin is null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin.Id.ToString()),
            new(ClaimTypes.Name, admin.Email),
            new(ClaimTypes.Role, admin.Role.ToString()),
            new(SuperAdminClaims.MustChangePassword, admin.MustChangePassword ? "true" : "false")
        };
        var identity = new ClaimsIdentity(claims, AuthSchemes.SuperAdmin);
        await HttpContext.SignInAsync(AuthSchemes.SuperAdmin, new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        await _superAdmins.RecordLoginAsync(admin.Id, ct);

        if (admin.MustChangePassword)
            return RedirectToAction(nameof(ChangePassword));

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" });
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.SuperAdmin);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("change-password")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    public IActionResult ChangePassword()
    {
        // Uses the auth layout (no shell) since the user may be mid-bootstrap.
        ViewData["Layout"] = "_AuthLayout";
        return View(new ChangePasswordViewModel());
    }

    [HttpPost("change-password")]
    [Authorize(Policy = AuthPolicies.SuperAdmin)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model, CancellationToken ct)
    {
        ViewData["Layout"] = "_AuthLayout";
        if (!ModelState.IsValid) return View(model);

        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _superAdmins.ChangePasswordAsync(id, model.NewPassword, ct);

        // Refresh the principal so the must-change claim is cleared.
        var admin = await _superAdmins.GetAsync(id, ct)!;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, admin!.Id.ToString()),
            new(ClaimTypes.Name, admin.Email),
            new(ClaimTypes.Role, admin.Role.ToString()),
            new(SuperAdminClaims.MustChangePassword, "false")
        };
        await HttpContext.SignInAsync(AuthSchemes.SuperAdmin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.SuperAdmin)),
            new AuthenticationProperties { IsPersistent = true });

        TempData["Success"] = "Your password has been updated.";
        return RedirectToAction("Index", "Dashboard", new { area = "SuperAdmin" });
    }
}
