using Authly.Modules.Account;
using Authly.Web.Areas.Portal.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal landing: view/edit profile and change password (Phase 10).</summary>
[Route("portal/profile")]
public sealed class ProfileController : PortalControllerBase
{
    private readonly IAccountSelfService _account;

    public ProfileController(IAccountSelfService account) => _account = account;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Profile";
        var user = await _account.GetAsync(TenantId, UserId, ct);
        if (user is null) return NotFound();

        return View(new PortalProfileViewModel
        {
            Email = user.Email,
            EmailVerified = user.EmailVerified,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Timezone = user.Timezone,
            Locale = user.Locale
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(PortalProfileViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Profile";
        if (!ModelState.IsValid) return View(model);

        await _account.UpdateProfileAsync(TenantId, UserId,
            new ProfileUpdate(model.FirstName, model.LastName, model.Timezone, model.Locale), CurrentAudit(), ct);

        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Index));
    }

    // --- password ----------------------------------------------------------

    [HttpGet("password")]
    public async Task<IActionResult> ChangePassword(CancellationToken ct)
    {
        ViewData["Title"] = "Change password";
        var user = await _account.GetAsync(TenantId, UserId, ct);
        if (user is null) return NotFound();
        return View(new PortalChangePasswordViewModel { HasPassword = user.PasswordHash is not null });
    }

    [HttpPost("password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(PortalChangePasswordViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Change password";
        if (!ModelState.IsValid) return View(model);

        var result = await _account.ChangePasswordAsync(TenantId, UserId, model.CurrentPassword,
            model.NewPassword, CurrentSessionId, CurrentAudit(), ct);

        if (result == PasswordChangeResult.WrongCurrentPassword)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), "That current password is incorrect.");
            return View(model);
        }

        TempData["Success"] = "Password changed. Other devices have been signed out.";
        return RedirectToAction(nameof(Index));
    }
}
