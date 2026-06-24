using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Account;
using Authly.Modules.AdvancedAuth;
using Authly.Modules.Common;
using Authly.Modules.Mfa;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal: secure change of the signed-in user's email or phone (Phase 11). Email uses the
/// link-based two-step confirmation; phone uses a WhatsApp OTP (enter the code to verify).</summary>
[Route("portal/contact")]
public sealed class ContactController : PortalControllerBase
{
    private readonly IContactChangeService _contactChange;
    private readonly IAccountSelfService _account;
    private readonly IUserRepository _users;
    private readonly IMfaService _mfa;

    public ContactController(IContactChangeService contactChange, IAccountSelfService account,
        IUserRepository users, IMfaService mfa)
    {
        _contactChange = contactChange;
        _account = account;
        _users = users;
        _mfa = mfa;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Email & phone";
        var user = await _account.GetAsync(TenantId, UserId, ct);
        if (user is null) return NotFound();
        ViewData["Email"] = user.Email;
        ViewData["Phone"] = user.Phone;
        ViewData["PhoneVerified"] = user.PhoneVerified;
        return View();
    }

    [HttpPost("email")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeEmail(string newEmail, CancellationToken ct)
    {
        try
        {
            var outcome = await _contactChange.RequestChangeAsync(TenantId, UserId, ContactType.Email, newEmail ?? "", CurrentRequest(), ct);
            TempData[outcome == ContactChangeOutcome.Started ? "Success" : "Error"] = outcome switch
            {
                ContactChangeOutcome.Started => "Check your new email for a confirmation link. We've also alerted your current email.",
                ContactChangeOutcome.AlreadyInUse => "That email is already in use.",
                ContactChangeOutcome.Cooldown => "A change is already pending. Please wait a moment before trying again.",
                _ => "Could not start the change."
            };
        }
        catch (AdvancedAuthException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Sets the new phone (unverified) and sends a WhatsApp OTP to it; the user confirms on
    /// the verify screen. No link — a correct code marks the number verified.</summary>
    [HttpPost("phone")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePhone(string newPhone, CancellationToken ct)
    {
        var normalized = PhoneNumber.Normalize(newPhone);
        if (string.IsNullOrEmpty(normalized))
        {
            TempData["Error"] = "Enter a valid phone number.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _users.GetByIdAsync(TenantId, UserId, ct);
        if (user is null) return NotFound();

        var holder = await _users.GetByPhoneAsync(TenantId, normalized, ct);
        if (holder is not null && holder.Id != UserId)
        {
            TempData["Error"] = "That phone number is already in use.";
            return RedirectToAction(nameof(Index));
        }

        user.Phone = normalized;
        user.PhoneVerified = false;
        await _users.UpdateAsync(user, ct);
        await _mfa.SendPhoneOtpAsync(TenantId, user, ct);

        TempData["PhoneToVerify"] = normalized;
        return RedirectToAction(nameof(VerifyPhone));
    }

    [HttpGet("phone/verify")]
    public async Task<IActionResult> VerifyPhone(CancellationToken ct)
    {
        ViewData["Title"] = "Verify your phone";
        var user = await _users.GetByIdAsync(TenantId, UserId, ct);
        if (user is null || string.IsNullOrEmpty(user.Phone)) return RedirectToAction(nameof(Index));
        if (user.PhoneVerified) return RedirectToAction(nameof(Index));
        ViewData["Phone"] = user.Phone;
        return View();
    }

    [HttpPost("phone/verify")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPhone(string code, CancellationToken ct)
    {
        ViewData["Title"] = "Verify your phone";
        var user = await _users.GetByIdAsync(TenantId, UserId, ct);
        if (user is null || string.IsNullOrEmpty(user.Phone)) return RedirectToAction(nameof(Index));
        ViewData["Phone"] = user.Phone;

        var ok = await _mfa.VerifyPhoneOtpAsync(TenantId, UserId, code ?? "", CurrentAudit(), ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "That code is invalid or has expired.");
            return View();
        }

        user.PhoneVerified = true;
        await _users.UpdateAsync(user, ct);
        TempData["Success"] = "Your phone number is verified.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Re-send the WhatsApp OTP to the pending (unverified) phone.</summary>
    [HttpPost("phone/resend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendPhoneOtp(CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(TenantId, UserId, ct);
        if (user is not null && !string.IsNullOrEmpty(user.Phone) && !user.PhoneVerified)
            await _mfa.SendPhoneOtpAsync(TenantId, user, ct);
        return RedirectToAction(nameof(VerifyPhone));
    }

    private RequestInfo CurrentRequest()
        => new(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
