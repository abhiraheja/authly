using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Mfa;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.Mfa;
using Authly.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// The MFA login gate. Reached only between a correct password and a completed second factor:
/// the user is NOT signed in here — state lives in the data-protected <see cref="MfaPendingStore"/>
/// cookie. Clearing the gate is what finally issues the user session cookie.
/// </summary>
[Route("account/mfa")]
public sealed class MfaController : Controller
{
    private readonly IMfaService _mfa;
    private readonly IUserRepository _users;
    private readonly ITenantContext _tenant;
    private readonly MfaPendingStore _pending;

    public MfaController(IMfaService mfa, IUserRepository users, ITenantContext tenant, MfaPendingStore pending)
    {
        _mfa = mfa;
        _users = users;
        _tenant = tenant;
        _pending = pending;
    }

    // --- Challenge (user already has factors) ------------------------------

    [HttpGet("")]
    public async Task<IActionResult> Challenge(CancellationToken ct)
    {
        if (Resolve() is not { } p) return RedirectToLogin();
        ViewData["Title"] = "Two-step verification";
        return View(new MfaChallengeViewModel { Methods = await _mfa.GetAvailableMethodsAsync(p.TenantId, p.UserId, ct) });
    }

    [HttpPost("totp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyTotp(string? code, CancellationToken ct)
    {
        if (Resolve() is not { } p) return RedirectToLogin();
        return await VerifyAsync(p, () => _mfa.VerifyTotpAsync(p.TenantId, p.UserId, code ?? "", Audit(), ct), ct);
    }

    [HttpPost("backup")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyBackup(string? code, CancellationToken ct)
    {
        if (Resolve() is not { } p) return RedirectToLogin();
        return await VerifyAsync(p, () => _mfa.VerifyBackupCodeAsync(p.TenantId, p.UserId, code ?? "", Audit(), ct), ct);
    }

    [HttpPost("email/send")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendEmail(CancellationToken ct)
    {
        if (Resolve() is not { } p) return RedirectToLogin();
        ViewData["Title"] = "Two-step verification";

        var user = await _users.GetByIdAsync(p.TenantId, p.UserId, ct);
        if (user is not null)
            await _mfa.SendEmailOtpAsync(p.TenantId, user, ct);

        return View("Challenge", new MfaChallengeViewModel
        {
            Methods = await _mfa.GetAvailableMethodsAsync(p.TenantId, p.UserId, ct),
            EmailOtpSent = true
        });
    }

    [HttpPost("email")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyEmail(string? code, CancellationToken ct)
    {
        if (Resolve() is not { } p) return RedirectToLogin();
        return await VerifyAsync(p, () => _mfa.VerifyEmailOtpAsync(p.TenantId, p.UserId, code ?? "", Audit(), ct), ct, emailSent: true);
    }

    // --- Forced enrolment (policy requires MFA, user has none) --------------

    [HttpGet("enroll")]
    public async Task<IActionResult> Enroll(CancellationToken ct)
    {
        if (Resolve() is not { } p) return RedirectToLogin();
        ViewData["Title"] = "Set up two-step verification";

        var enrollment = await _mfa.BeginTotpEnrollmentAsync(p.TenantId, p.UserId, p.Email, null, ct);
        return View(new MfaEnrollViewModel
        {
            FactorId = enrollment.FactorId,
            Secret = enrollment.Secret,
            QrSvg = QrCodeRenderer.SvgFromText(enrollment.ProvisioningUri)
        });
    }

    [HttpPost("enroll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(MfaEnrollViewModel model, CancellationToken ct)
    {
        if (Resolve() is not { } p) return RedirectToLogin();
        ViewData["Title"] = "Set up two-step verification";

        var ok = await _mfa.ConfirmTotpEnrollmentAsync(p.TenantId, p.UserId, model.FactorId, model.Code, Audit(), ct);
        if (!ok)
        {
            // Re-issue a fresh secret so the user isn't stuck with a code they can't match.
            var enrollment = await _mfa.BeginTotpEnrollmentAsync(p.TenantId, p.UserId, p.Email, null, ct);
            model.FactorId = enrollment.FactorId;
            model.Secret = enrollment.Secret;
            model.QrSvg = QrCodeRenderer.SvgFromText(enrollment.ProvisioningUri);
            model.Error = "That code didn't match. Scan the new QR code and try again.";
            return View(model);
        }

        return await CompleteAsync(p);
    }

    // --- helpers ------------------------------------------------------------

    private async Task<IActionResult> VerifyAsync(MfaPendingLogin p, Func<Task<bool>> verify, CancellationToken ct, bool emailSent = false)
    {
        ViewData["Title"] = "Two-step verification";
        if (await verify())
            return await CompleteAsync(p);

        return View("Challenge", new MfaChallengeViewModel
        {
            Methods = await _mfa.GetAvailableMethodsAsync(p.TenantId, p.UserId, ct),
            EmailOtpSent = emailSent,
            Error = "That code is incorrect or has expired. Please try again."
        });
    }

    private async Task<IActionResult> CompleteAsync(MfaPendingLogin p)
    {
        await UserSignIn.SignInAsync(HttpContext, p.UserId, p.Email, p.TenantId, p.SessionId, p.EmailVerified);
        _pending.Clear(HttpContext);

        if (p.ReturnUrl is not null && Url.IsLocalUrl(p.ReturnUrl))
            return Redirect(p.ReturnUrl);
        return RedirectToAction(nameof(AccountController.Index), "Account");
    }

    /// <summary>Reads the pending state and enforces it matches the host-resolved tenant.</summary>
    private MfaPendingLogin? Resolve()
    {
        var pending = _pending.Read(HttpContext);
        if (pending is null) return null;
        if (!_tenant.HasTenant || _tenant.TenantId != pending.TenantId)
        {
            _pending.Clear(HttpContext);
            return null;
        }
        return pending;
    }

    private IActionResult RedirectToLogin() => RedirectToAction(nameof(AccountController.Login), "Account");

    private AuditContext Audit() => new(
        null, "user",
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
