using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Modules.Mfa;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.Mfa;
using Authly.Web.Models;
using Hangfire;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// Tenant end-user authentication surface (register / login / email verification / password
/// reset). Every action is tenant-scoped via <see cref="ITenantContext"/>; requests that
/// arrive without a resolved tenant get a clear prompt rather than leaking into another tenant.
/// </summary>
[Route("account")]
public sealed class AccountController : Controller
{
    private readonly IAuthService _auth;
    private readonly ITenantContext _tenant;
    private readonly IMfaService _mfa;
    private readonly MfaPendingStore _mfaPending;
    private readonly Authly.Modules.Social.ISocialLoginService _social;
    private readonly Authly.Modules.AdvancedAuth.IMagicLinkService _magic;
    private readonly Authly.Modules.AdvancedAuth.IContactChangeService _contactChange;
    private readonly Authly.Modules.AdvancedAuth.IRecoveryService _recovery;
    private readonly Authly.Modules.Security.IAccountLockoutService _lockout;
    private readonly Authly.Modules.Security.ISecurityScreeningService _screening;
    private readonly Authly.Modules.Security.IBlockListService _blockList;
    private readonly Authly.Web.Infrastructure.Security.SecurityViewState _securityView;
    private readonly Authly.Modules.Compliance.IConsentService _consent;
    private readonly Hangfire.IBackgroundJobClient _jobs;

    public AccountController(IAuthService auth, ITenantContext tenant, IMfaService mfa, MfaPendingStore mfaPending,
        Authly.Modules.Social.ISocialLoginService social,
        Authly.Modules.AdvancedAuth.IMagicLinkService magic,
        Authly.Modules.AdvancedAuth.IContactChangeService contactChange,
        Authly.Modules.AdvancedAuth.IRecoveryService recovery,
        Authly.Modules.Security.IAccountLockoutService lockout,
        Authly.Modules.Security.ISecurityScreeningService screening,
        Authly.Modules.Security.IBlockListService blockList,
        Authly.Web.Infrastructure.Security.SecurityViewState securityView,
        Authly.Modules.Compliance.IConsentService consent,
        Hangfire.IBackgroundJobClient jobs)
    {
        _auth = auth;
        _tenant = tenant;
        _mfa = mfa;
        _mfaPending = mfaPending;
        _social = social;
        _magic = magic;
        _contactChange = contactChange;
        _recovery = recovery;
        _lockout = lockout;
        _screening = screening;
        _blockList = blockList;
        _securityView = securityView;
        _consent = consent;
        _jobs = jobs;
    }

    // --- Registration ---

    [HttpGet("register")]
    public async Task<IActionResult> Register(CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Create your account";
        await PopulateCaptchaAsync(ct);
        return View(new RegisterViewModel());
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Create your account";
        await PopulateCaptchaAsync(ct);
        if (!ModelState.IsValid) return View(model);

        // Bot defence + block lists + breached-password screen before we create anything.
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var screen = await _screening.ScreenRegistrationAsync(_tenant.TenantId!.Value,
            model.Email, model.Password, CaptchaToken(), ip, ct);
        if (!screen.Passed)
        {
            if (screen.CaptchaFailed) ModelState.AddModelError(string.Empty, "Please complete the verification challenge.");
            if (screen.IpBlocked) ModelState.AddModelError(string.Empty, "Sign-up isn't available from your network.");
            if (screen.EmailBlocked) ModelState.AddModelError(nameof(model.Email), "Sign-ups from this email domain aren't allowed.");
            if (screen.PasswordBreached) ModelState.AddModelError(nameof(model.Password), "This password has appeared in a data breach. Choose a different one.");
            return View(model);
        }

        try
        {
            var user = await _auth.RegisterAsync(_tenant.TenantId!.Value,
                new RegisterRequest(model.Email, model.Password, model.FirstName, model.LastName),
                CurrentRequest(), ct);

            // GDPR/DPDP: record the terms + privacy consent the user gave at signup.
            await _consent.RecordSignupConsentAsync(_tenant.TenantId!.Value, user.Id, policyVersion: null,
                new AuditContext(user.Id, "user", HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null), ct);
        }
        catch (EmailAlreadyExistsException)
        {
            // Don't confirm or deny existence beyond what the user already supplied.
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        TempData["Email"] = model.Email;
        return RedirectToAction(nameof(RegistrationConfirmation));
    }

    [HttpGet("registered")]
    public IActionResult RegistrationConfirmation()
    {
        ViewData["Title"] = "Check your email";
        ViewData["Email"] = TempData["Email"];
        return View();
    }

    // --- Login / logout ---

    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken ct = default)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Sign in";
        ViewData["SocialOptions"] = await _social.ListActiveOptionsAsync(_tenant.TenantId!.Value, ct);
        await PopulateCaptchaAsync(ct);
        return View(new UserLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(UserLoginViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Sign in";
        await PopulateCaptchaAsync(ct);
        if (!ModelState.IsValid) return View(model);

        var tenantId = _tenant.TenantId!.Value;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Network allow/block list.
        if (!await _blockList.IsIpAllowedAsync(tenantId, ip, ct) || await _blockList.IsIpBlockedAsync(tenantId, ip, ct))
        {
            ModelState.AddModelError(string.Empty, "Sign-in isn't available from your network.");
            return View(model);
        }

        // Account lockout (brute-force defence).
        if (await _lockout.IsLockedAsync(tenantId, model.Email, ct))
        {
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Try again later or reset your password.");
            return View(model);
        }

        // Bot defence.
        if (!await _screening.VerifyCaptchaAsync(tenantId, CaptchaToken(), ip, ct))
        {
            ModelState.AddModelError(string.Empty, "Please complete the verification challenge.");
            return View(model);
        }

        var result = await _auth.AuthenticateAsync(tenantId, model.Email, model.Password, CurrentRequest(), ct);

        if (!result.Succeeded || result.User is null || result.Session is null)
        {
            await _lockout.RecordFailureAsync(tenantId, model.Email, ct);
            // Same message for bad credentials and suspended accounts (no account-state leak).
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // Successful credential check — clear lockout and analyse the login for anomalies.
        await _lockout.ResetAsync(tenantId, model.Email, ct);
        QueueSuspiciousLoginCheck(result.User.Id);

        var returnUrl = !string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? model.ReturnUrl : null;

        // Password is correct, but the session cookie is only issued once any MFA gate is cleared.
        var decision = await _mfa.EvaluateLoginAsync(_tenant.TenantId!.Value, result.User, ct);
        if (decision.Requirement != MfaLoginRequirement.NotRequired)
        {
            _mfaPending.Save(HttpContext, new MfaPendingLogin(
                result.User.Id, result.User.TenantId, result.Session.Id,
                result.User.Email, result.User.EmailVerified, returnUrl));

            return decision.Requirement == MfaLoginRequirement.EnrollmentRequired
                ? RedirectToAction(nameof(MfaController.Enroll), "Mfa")
                : RedirectToAction(nameof(MfaController.Challenge), "Mfa");
        }

        await UserSignIn.SignInAsync(HttpContext, result.User.Id, result.User.Email, result.User.TenantId,
            result.Session.Id, result.User.EmailVerified);

        return returnUrl is not null ? Redirect(returnUrl) : Portal();
    }

    [HttpPost("logout")]
    [Authorize(Policy = AuthPolicies.User)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var sessionId = User.FindFirstValue(UserClaims.SessionId);
        if (Guid.TryParse(sessionId, out var id))
            await _auth.RevokeSessionAsync(id, ct);

        await HttpContext.SignOutAsync(AuthSchemes.User);
        return RedirectToAction(nameof(Login));
    }

    // --- Authenticated landing ---

    [HttpGet("")]
    [Authorize(Policy = AuthPolicies.User)]
    public IActionResult Index() => Portal();

    /// <summary>The signed-in landing is the end-user portal (Phase 10).</summary>
    private IActionResult Portal() => RedirectToAction("Index", "Profile", new { area = "Portal" });

    // --- Email verification ---

    [HttpGet("verify-email")]
    public async Task<IActionResult> VerifyEmail(string? token, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Email verification";
        ViewData["Verified"] = !string.IsNullOrWhiteSpace(token)
            && await _auth.VerifyEmailAsync(_tenant.TenantId!.Value, token!, ct);
        return View();
    }

    // --- Password reset ---

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword()
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Forgot password";
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost("forgot-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Forgot password";
        if (!ModelState.IsValid) return View(model);

        await _auth.RequestPasswordResetAsync(_tenant.TenantId!.Value, model.Email, CurrentRequest(), ct);

        // Always the same response, whether or not the email exists (anti-enumeration).
        ViewData["Email"] = model.Email;
        return View("ForgotPasswordConfirmation");
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword(string? token)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Reset password";
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewData["InvalidToken"] = true;
            return View(new ResetPasswordViewModel());
        }
        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost("reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Reset password";
        if (!ModelState.IsValid) return View(model);

        if (await _screening.IsPasswordBreachedAsync(_tenant.TenantId!.Value, model.NewPassword, ct))
        {
            ModelState.AddModelError(nameof(model.NewPassword), "This password has appeared in a data breach. Choose a different one.");
            return View(model);
        }

        var ok = await _auth.ResetPasswordAsync(_tenant.TenantId!.Value, model.Token, model.NewPassword, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired. Request a new one.");
            return View(model);
        }

        TempData["Success"] = "Your password has been reset. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    // --- Magic link (passwordless) ---

    [HttpGet("magic-link")]
    public IActionResult MagicLink()
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Email me a sign-in link";
        return View(new EmailOnlyViewModel());
    }

    [HttpPost("magic-link")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MagicLink(EmailOnlyViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Email me a sign-in link";
        if (!ModelState.IsValid) return View(model);

        await _magic.RequestAsync(_tenant.TenantId!.Value, model.Email, CurrentRequest(), ct);
        // Always the same response, whether or not the account exists (anti-enumeration).
        ViewData["Email"] = model.Email;
        return View("MagicLinkSent");
    }

    [HttpGet("magic")]
    public async Task<IActionResult> Magic(string? token, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;

        var user = string.IsNullOrWhiteSpace(token)
            ? null
            : await _magic.CompleteAsync(_tenant.TenantId!.Value, token!, ct);

        if (user is null)
        {
            TempData["Error"] = "This sign-in link is invalid or has expired. Request a new one.";
            return RedirectToAction(nameof(Login));
        }

        var session = await _auth.StartSessionAsync(user, "magic_link", CurrentRequest(), ct);
        QueueSuspiciousLoginCheck(user.Id);
        await UserSignIn.SignInAsync(HttpContext, user.Id, user.Email, user.TenantId, session.Id, user.EmailVerified);
        return Portal();
    }

    // --- Account recovery ---

    [HttpGet("recover-request")]
    public IActionResult RecoverRequest()
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Recover your account";
        return View(new EmailOnlyViewModel());
    }

    [HttpPost("recover-request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecoverRequest(EmailOnlyViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Recover your account";
        if (!ModelState.IsValid) return View(model);

        await _recovery.InitiateRecoveryAsync(_tenant.TenantId!.Value, model.Email, CurrentRequest(), ct);
        // Anti-enumeration: same confirmation whether or not the account exists.
        ViewData["Email"] = model.Email;
        return View("RecoverSent");
    }

    // The recovery link lands here and lets the user set a new password (recovery issues a reset token).
    [HttpGet("recover")]
    public IActionResult Recover(string? token)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Set a new password";
        if (string.IsNullOrWhiteSpace(token))
        {
            ViewData["InvalidToken"] = true;
            return View(new ResetPasswordViewModel());
        }
        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost("recover")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Recover(ResetPasswordViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Set a new password";
        if (!ModelState.IsValid) return View(model);

        if (await _screening.IsPasswordBreachedAsync(_tenant.TenantId!.Value, model.NewPassword, ct))
        {
            ModelState.AddModelError(nameof(model.NewPassword), "This password has appeared in a data breach. Choose a different one.");
            return View(model);
        }

        var ok = await _auth.ResetPasswordAsync(_tenant.TenantId!.Value, model.Token, model.NewPassword, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "This recovery link is invalid or has expired. Request a new one.");
            return View(model);
        }

        TempData["Success"] = "Your password has been set. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    // --- Email/phone change (links from the confirmation + cancel emails) ---

    [HttpGet("change/verify")]
    public async Task<IActionResult> VerifyContactChange(string? token, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Contact change";
        ViewData["Verified"] = !string.IsNullOrWhiteSpace(token)
            && await _contactChange.VerifyAsync(_tenant.TenantId!.Value, token!, ct);
        return View("ContactChangeResult");
    }

    [HttpGet("change/cancel")]
    public async Task<IActionResult> CancelContactChange(string? token, CancellationToken ct)
    {
        ViewData["Title"] = "Contact change";
        // Cancellation works from the OLD address even if signed out; the token is the proof.
        ViewData["Cancelled"] = !string.IsNullOrWhiteSpace(token)
            && await _contactChange.CancelAsync(token!, ct);
        ViewData["IsCancel"] = true;
        return View("ContactChangeResult");
    }

    // --- helpers ---

    /// <summary>Returns a view result when no tenant is resolved; null when a tenant is in scope.</summary>
    private IActionResult? RequireTenant()
        => _tenant.HasTenant ? null : View("TenantRequired");

    private RequestInfo CurrentRequest()
        => new(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);

    /// <summary>Reads the CAPTCHA token from whichever provider's form field is present.</summary>
    private string? CaptchaToken()
        => Request.Form["h-captcha-response"].FirstOrDefault()
           ?? Request.Form["cf-turnstile-response"].FirstOrDefault()
           ?? Request.Form["captcha-token"].FirstOrDefault();

    /// <summary>Loads the CAPTCHA widget (if enabled) into ViewData for the auth views.</summary>
    private async Task PopulateCaptchaAsync(CancellationToken ct)
    {
        if (_tenant.HasTenant)
            ViewData["Captcha"] = await _securityView.GetCaptchaAsync(_tenant.TenantId!.Value, ct);
    }

    /// <summary>Fire-and-forget background analysis of a successful login for new device/location.</summary>
    private void QueueSuspiciousLoginCheck(Guid userId)
    {
        var tenantId = _tenant.TenantId!.Value;
        var info = CurrentRequest();
        _jobs.Enqueue<Authly.Web.Infrastructure.Security.SuspiciousLoginJob>(
            j => j.EvaluateAsync(tenantId, userId, info.IpAddress, info.UserAgent));
    }
}
