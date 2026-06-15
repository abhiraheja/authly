using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Mfa;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.Mfa;
using Authly.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// End-user self-service security: enrol/disable MFA factors, enable email OTP, and (re)generate
/// backup codes. Authenticated by the user cookie and scoped to the signed-in user only.
/// </summary>
[Route("account/security")]
[Authorize(Policy = AuthPolicies.User)]
public sealed class SecurityController : Controller
{
    private readonly IMfaService _mfa;
    private readonly IUserRepository _users;
    private readonly ITenantContext _tenant;

    public SecurityController(IMfaService mfa, IUserRepository users, ITenantContext tenant)
    {
        _mfa = mfa;
        _users = users;
        _tenant = tenant;
    }

    private Guid TenantId => Guid.Parse(User.FindFirstValue(UserClaims.TenantId)!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

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
        var email = User.FindFirstValue(ClaimTypes.Name) ?? "user";
        var enrollment = await _mfa.BeginTotpEnrollmentAsync(TenantId, UserId, email, null, ct);
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

        if (await _mfa.ConfirmTotpEnrollmentAsync(TenantId, UserId, model.FactorId, model.Code, Audit(), ct))
        {
            TempData["Success"] = "Authenticator app added. Generate backup codes if you haven't already.";
            return RedirectToAction(nameof(Index));
        }

        var email = User.FindFirstValue(ClaimTypes.Name) ?? "user";
        var enrollment = await _mfa.BeginTotpEnrollmentAsync(TenantId, UserId, email, model.FriendlyName, ct);
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
                await _mfa.EnableEmailOtpAsync(TenantId, user, Audit(), ct);
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
        var result = await _mfa.GenerateBackupCodesAsync(TenantId, UserId, Audit(), ct);
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
            await _mfa.DisableFactorAsync(TenantId, UserId, id, Audit(), ct);
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
            HasTotp = factors.Any(f => f.Type == Core.Enums.MfaFactorType.Totp && f.Status == Core.Enums.MfaFactorStatus.Active),
            HasEmailOtp = factors.Any(f => f.Type == Core.Enums.MfaFactorType.EmailOtp && f.Status == Core.Enums.MfaFactorStatus.Active)
        };
    }

    private AuditContext Audit() => new(
        UserId, "user",
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
