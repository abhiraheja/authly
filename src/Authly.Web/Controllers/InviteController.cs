using System.Security.Claims;
using Authly.Modules.Common;
using Authly.Modules.Members;
using Authly.Web.Infrastructure;
using Authly.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// Public, tenant-less employee-invite acceptance. An invitee opens the emailed link, sets a password
/// (if their account is new), and is signed straight into the console as their global Account — the
/// same claim shape as signup/login. Tenant-less by design: <see cref="TenantResolutionMiddleware"/>
/// excludes <c>/invite</c> so acceptance is not bound to any ambient tenant (doc 06 §7).
/// </summary>
[Route("invite")]
[AllowAnonymous]
public sealed class InviteController : Controller
{
    private readonly IInvitationService _invites;

    public InviteController(IInvitationService invites) => _invites = invites;

    [HttpGet("accept")]
    public async Task<IActionResult> Accept(string? token, CancellationToken ct)
    {
        ViewData["Layout"] = "_AuthLayout";
        var pending = string.IsNullOrWhiteSpace(token) ? null : await _invites.FindPendingAsync(token, ct);
        if (pending is null)
            return View("Invalid");

        var account = pending.Account;
        return View(new AcceptInviteViewModel
        {
            Token = token!,
            Email = account?.Email ?? string.Empty,
            RequiresPassword = string.IsNullOrEmpty(account?.PasswordHash)
        });
    }

    [HttpPost("accept")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(AcceptInviteViewModel model, CancellationToken ct)
    {
        ViewData["Layout"] = "_AuthLayout";

        // A brand-new account must set a password; an existing account keeps its own (no field shown).
        if (model.RequiresPassword && string.IsNullOrWhiteSpace(model.Password))
            ModelState.AddModelError(nameof(model.Password), "Choose a password to finish setting up your account.");
        if (!ModelState.IsValid) return View(model);

        var info = new RequestInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);

        AcceptInviteResult? result;
        try
        {
            result = await _invites.AcceptAsync(model.Token, model.Password, info, ct);
        }
        catch (InviteAccountException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        if (result is null)
            return View("Invalid");

        // Sign the operator straight into the console (same claim shape as signup / admin login).
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.Account.Id.ToString()),
            new(ClaimTypes.Name, result.Account.Email),
            new(TenantAdminClaims.AccountId, result.Account.Id.ToString()),
            new(TenantAdminClaims.OrgId, result.OrganizationId.ToString()),
            new(TenantAdminClaims.TenantId, result.ProjectId.ToString())
        };
        await HttpContext.SignInAsync(AuthSchemes.TenantAdmin,
            new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.TenantAdmin)),
            new AuthenticationProperties { IsPersistent = true });

        TempData["Success"] = "You're in — welcome to the team.";
        return RedirectToAction("Index", "Applications", new { area = "TenantAdmin" });
    }
}
