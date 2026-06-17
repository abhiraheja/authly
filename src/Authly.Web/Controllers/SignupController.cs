using System.Security.Claims;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Modules.Signup;
using Authly.Web.Infrastructure;
using Authly.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// Public, self-service workspace signup (the Supabase / Google-Console model): a visitor creates a
/// tenant and becomes its first administrator, then is signed straight into the tenant-admin surface.
/// Tenant-less by design — <see cref="TenantResolutionMiddleware"/> excludes this path from host
/// resolution so the new workspace is provisioned without an ambient tenant.
/// </summary>
[Route("signup")]
[AllowAnonymous]
public sealed class SignupController : Controller
{
    private readonly ITenantSignupService _signup;

    public SignupController(ITenantSignupService signup) => _signup = signup;

    [HttpGet("")]
    public IActionResult Index() => View(new SignupViewModel());

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SignupViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        var info = new RequestInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);

        TenantSignupResult result;
        try
        {
            result = await _signup.SignUpAsync(
                new TenantSignupRequest(model.CompanyName, model.Email, model.Password, model.FirstName, model.LastName),
                info, ct);
        }
        catch (EmailAlreadyExistsException)
        {
            // A fresh workspace has no users, so this only fires on an unexpected race; keep it generic.
            ModelState.AddModelError(nameof(model.Email), "That email could not be used. Please try another.");
            return View(model);
        }
        catch (TenantSignupException ex)
        {
            ModelState.AddModelError(nameof(model.CompanyName), ex.Message);
            return View(model);
        }

        // Sign the founding owner straight into the console as their global Account, scoped to the
        // new org and its first project (same claim shape as the account-based admin login).
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Account.Id.ToString()),
            new(ClaimTypes.Name, result.Account.Email),
            new(TenantAdminClaims.AccountId, result.Account.Id.ToString()),
            new(TenantAdminClaims.OrgId, result.Organization.Id.ToString()),
            new(TenantAdminClaims.TenantId, result.Tenant.Id.ToString())
        };
        await HttpContext.SignInAsync(AuthSchemes.TenantAdmin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.TenantAdmin)),
            new AuthenticationProperties { IsPersistent = true });

        TempData["Success"] = "Your workspace is ready — welcome to Authly.";
        return RedirectToAction("Index", "Onboarding", new { area = "TenantAdmin" });
    }
}
