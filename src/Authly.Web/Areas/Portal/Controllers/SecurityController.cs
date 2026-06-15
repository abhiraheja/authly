using System.Security.Claims;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Mfa;
using Authly.Web.Infrastructure.Mfa;
using Authly.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>
/// Portal: end-user self-service MFA — enrol/disable authenticator (TOTP), enable email OTP, and
/// (re)generate backup codes (Phase 10; moved here from /account/security).
/// </summary>
[Route("portal/security")]
public sealed class SecurityController : PortalControllerBase
{
    private readonly IMfaService _mfa;
    private readonly IUserRepository _users;

    public SecurityController(IMfaService mfa, IUserRepository users)
    {
        _mfa = mfa;
        _users = users;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Security";
        return View(await BuildOverviewAsync(ct));
    }

    // --- TOTP ---------------------------------------------------------------

    [HttpGet("totp")]
    public async Task<IActionResult> Totp(CancellationToken ct)
    {
        ViewData["Title"] = "Add authenticator app";
        var enrollment = await _mfa.BeginTotpEnrollmentAsync(TenantId, UserId, UserEmail, null, ct);
        return View(new SecurityTotpSetupViewModel
        {
            FactorId = enrollment.FactorId,
            Secret = enrollment.Secret,
            QrSvg = QrCodeRenderer.SvgFromText(enrollment.ProvisioningUri)
        });
    }

    [HttpPost("totp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Totp(SecurityTotpSetupViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Add authenticator app";

        if (await _mfa.ConfirmTotpEnrollmentAsync(TenantId, UserId, model.FactorId, model.Code, CurrentAudit(), ct))
        {
            TempData["Success"] = "Authenticator app added. Generate backup codes if you haven't already.";
            return RedirectToAction(nameof(Index));
        }

        var enrollment = await _mfa.BeginTotpEnrollmentAsync(TenantId, UserId, UserEmail, model.FriendlyName, ct);
        model.FactorId = enrollment.FactorId;
        model.Secret = enrollment.Secret;
        model.QrSvg = QrCodeRenderer.SvgFromText(enrollment.ProvisioningUri);
        model.Error = "That code didn't match. Scan the new QR code and try again.";
        return View(model);
    }

    // --- Email OTP ----------------------------------------------------------

    [HttpPost("email-otp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnableEmailOtp(CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(TenantId, UserId, ct);
        if (user is not null)
        {
            try
            {
                await _mfa.EnableEmailOtpAsync(TenantId, user, CurrentAudit(), ct);
                TempData["Success"] = "Email one-time codes are now enabled.";
            }
            catch (MfaMethodNotAllowedException)
            {
                TempData["Error"] = "Email codes are not permitted by your organization.";
            }
        }
        return RedirectToAction(nameof(Index));
    }

    // --- Backup codes -------------------------------------------------------

    [HttpPost("backup-codes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BackupCodes(CancellationToken ct)
    {
        var result = await _mfa.GenerateBackupCodesAsync(TenantId, UserId, CurrentAudit(), ct);
        var overview = await BuildOverviewAsync(ct);
        overview.NewBackupCodes = result.Codes;
        ViewData["Title"] = "Security";
        return View(nameof(Index), overview);
    }

    // --- Disable a factor ---------------------------------------------------

    [HttpPost("factors/{id:guid}/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableFactor(Guid id, CancellationToken ct)
    {
        try
        {
            await _mfa.DisableFactorAsync(TenantId, UserId, id, CurrentAudit(), ct);
            TempData["Success"] = "Factor removed.";
        }
        catch (MfaFactorNotFoundException)
        {
            TempData["Error"] = "That factor no longer exists.";
        }
        return RedirectToAction(nameof(Index));
    }

    // --- helpers ------------------------------------------------------------

    private async Task<SecurityOverviewViewModel> BuildOverviewAsync(CancellationToken ct)
    {
        var factors = await _mfa.ListFactorsAsync(TenantId, UserId, ct);
        var policy = await _mfa.GetPolicyAsync(TenantId, ct);
        return new SecurityOverviewViewModel
        {
            Factors = factors,
            UnusedBackupCodes = await _mfa.CountUnusedBackupCodesAsync(UserId, ct),
            Policy = policy,
            HasTotp = factors.Any(f => f.Type == MfaFactorType.Totp && f.Status == MfaFactorStatus.Active),
            HasEmailOtp = factors.Any(f => f.Type == MfaFactorType.EmailOtp && f.Status == MfaFactorStatus.Active)
        };
    }
}
