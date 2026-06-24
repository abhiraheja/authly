using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
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
    private readonly IUserRepository _users;
    private readonly Authly.Modules.Messaging.IMessagingService _messaging;
    private readonly Authly.Web.Infrastructure.Auth.PhoneLoginPendingStore _phonePending;
    private readonly Authly.Modules.Social.ISocialLoginService _social;
    private readonly Authly.Modules.AdvancedAuth.IMagicLinkService _magic;
    private readonly Authly.Modules.AdvancedAuth.IContactChangeService _contactChange;
    private readonly Authly.Modules.AdvancedAuth.IRecoveryService _recovery;
    private readonly Authly.Modules.Security.IAccountLockoutService _lockout;
    private readonly Authly.Modules.Security.ISecurityScreeningService _screening;
    private readonly Authly.Modules.Security.IBlockListService _blockList;
    private readonly Authly.Modules.Security.IConditionalAccessService _conditional;
    private readonly Authly.Modules.Security.ISecuritySettingsService _securitySettings;
    private readonly Authly.Web.Infrastructure.Security.SecurityViewState _securityView;
    private readonly Authly.Modules.Compliance.IConsentService _consent;
    private readonly Authly.Modules.Users.IImpersonationService _impersonation;
    private readonly Authly.Modules.Devices.IDeviceService _devices;
    private readonly IAuditLogger _audit;
    private readonly Hangfire.IBackgroundJobClient _jobs;

    public AccountController(IAuthService auth, ITenantContext tenant, IMfaService mfa, MfaPendingStore mfaPending,
        IUserRepository users,
        Authly.Modules.Messaging.IMessagingService messaging,
        Authly.Web.Infrastructure.Auth.PhoneLoginPendingStore phonePending,
        Authly.Modules.Social.ISocialLoginService social,
        Authly.Modules.AdvancedAuth.IMagicLinkService magic,
        Authly.Modules.AdvancedAuth.IContactChangeService contactChange,
        Authly.Modules.AdvancedAuth.IRecoveryService recovery,
        Authly.Modules.Security.IAccountLockoutService lockout,
        Authly.Modules.Security.ISecurityScreeningService screening,
        Authly.Modules.Security.IBlockListService blockList,
        Authly.Modules.Security.IConditionalAccessService conditional,
        Authly.Modules.Security.ISecuritySettingsService securitySettings,
        Authly.Web.Infrastructure.Security.SecurityViewState securityView,
        Authly.Modules.Compliance.IConsentService consent,
        Authly.Modules.Users.IImpersonationService impersonation,
        Authly.Modules.Devices.IDeviceService devices,
        IAuditLogger audit,
        Hangfire.IBackgroundJobClient jobs)
    {
        _auth = auth;
        _tenant = tenant;
        _mfa = mfa;
        _mfaPending = mfaPending;
        _users = users;
        _messaging = messaging;
        _phonePending = phonePending;
        _social = social;
        _magic = magic;
        _contactChange = contactChange;
        _recovery = recovery;
        _lockout = lockout;
        _screening = screening;
        _blockList = blockList;
        _conditional = conditional;
        _securitySettings = securitySettings;
        _securityView = securityView;
        _consent = consent;
        _impersonation = impersonation;
        _devices = devices;
        _audit = audit;
        _jobs = jobs;
    }

    // --- Registration ---

    [HttpGet("register")]
    public async Task<IActionResult> Register(string? returnUrl = null, CancellationToken ct = default)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        if (!await SignupAllowedAsync(ct)) return SignupClosed(returnUrl);
        ViewData["Title"] = "Create your account";
        ViewData["AllowPhoneSignup"] = await PhoneAuthEnabledAsync(login: false, ct);
        await PopulateCaptchaAsync(ct);
        return View(new RegisterViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        // Server-side guard: the page may have been left open or the route hit directly.
        if (!await SignupAllowedAsync(ct)) return SignupClosed(model.ReturnUrl);
        ViewData["Title"] = "Create your account";
        ViewData["AllowPhoneSignup"] = await PhoneAuthEnabledAsync(login: false, ct);
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

        var tenantId = _tenant.TenantId!.Value;

        // Phone sign-up: only when enabled + WhatsApp ready, and the number must be free.
        var phoneSignup = await PhoneAuthEnabledAsync(login: false, ct);
        var normalizedPhone = phoneSignup ? Authly.Modules.Common.PhoneNumber.Normalize(model.Phone) : null;
        if (!string.IsNullOrEmpty(normalizedPhone)
            && await _users.GetByVerifiedPhoneAsync(tenantId, normalizedPhone, ct) is not null)
        {
            ModelState.AddModelError(nameof(model.Phone), "An account with this mobile number already exists.");
            return View(model);
        }

        Authly.Core.Entities.User user;
        try
        {
            user = await _auth.RegisterAsync(tenantId,
                new RegisterRequest(model.Email, model.Password, model.FirstName, model.LastName),
                CurrentRequest(), ct);

            // GDPR/DPDP: record the terms + privacy consent the user gave at signup.
            await _consent.RecordSignupConsentAsync(tenantId, user.Id, policyVersion: null,
                new AuditContext(user.Id, "user", HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null), ct);
        }
        catch (EmailAlreadyExistsException)
        {
            // Don't confirm or deny existence beyond what the user already supplied.
            ModelState.AddModelError(nameof(model.Email), "An account with this email already exists.");
            return View(model);
        }

        // If a phone was supplied, attach it (unverified) and send a WhatsApp OTP to verify it now,
        // so it can be used to sign in. Email verification proceeds via its link as usual.
        if (!string.IsNullOrEmpty(normalizedPhone))
        {
            user.Phone = normalizedPhone;
            user.PhoneVerified = false;
            await _users.UpdateAsync(user, ct);
            await _mfa.SendPhoneOtpAsync(tenantId, user, ct);
            _phonePending.Save(HttpContext, new Authly.Web.Infrastructure.Auth.PhonePendingOtp(
                user.Id, tenantId, "signup_verify", SafeReturnUrl(model.ReturnUrl)));
            TempData["PhoneOtpSent"] = true;
            return RedirectToAction(nameof(PhoneOtp), new { returnUrl = SafeReturnUrl(model.ReturnUrl) });
        }

        TempData["Email"] = model.Email;
        return RedirectToAction(nameof(RegistrationConfirmation), new { returnUrl = SafeReturnUrl(model.ReturnUrl) });
    }

    [HttpGet("registered")]
    public IActionResult RegistrationConfirmation(string? returnUrl = null)
    {
        ViewData["Title"] = "Check your email";
        ViewData["Email"] = TempData["Email"];
        ViewData["ReturnUrl"] = SafeReturnUrl(returnUrl);
        return View();
    }

    // --- Login / logout ---

    [HttpGet("login")]
    public async Task<IActionResult> Login(string? returnUrl = null, CancellationToken ct = default)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Sign in";
        ViewData["SocialOptions"] = await _social.ListActiveOptionsAsync(_tenant.TenantId!.Value, ct);
        ViewData["AllowPasswordSignup"] = await SignupAllowedAsync(ct);
        ViewData["AllowPhoneLogin"] = await PhoneAuthEnabledAsync(login: true, ct);
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

        // Successful credential check — clear lockout, record the device, analyse for anomalies.
        await _lockout.ResetAsync(tenantId, model.Email, ct);
        await _devices.RecordLoginAsync(tenantId, result.User.Id, CurrentRequest(), ct);
        QueueSuspiciousLoginCheck(result.User.Id);

        var returnUrl = SafeReturnUrl(model.ReturnUrl);

        // Risk-based access: block or force a step-up before the session cookie is issued.
        var access = await _conditional.EvaluateAsync(tenantId, result.User, CurrentRequest(), ct);
        if (access.Action == Authly.Modules.Security.ConditionalAction.Block)
        {
            await _auth.RevokeSessionAsync(result.Session.Id, ct);
            var actor = new AuditContext(result.User.Id, "user", ip, CurrentRequest().UserAgent);
            await _audit.LogAsync("user.login_blocked", actor, tenantId, "user", result.User.Id,
                result: "blocked", metadata: new { reason = access.Reason }, ct: ct);
            ModelState.AddModelError(string.Empty, "Sign-in was blocked by your organization's access policy.");
            return View(model);
        }
        var forceStepUp = access.Action == Authly.Modules.Security.ConditionalAction.RequireMfa;

        // Password is correct, but the session cookie is only issued once any MFA gate is cleared.
        var decision = await _mfa.EvaluateLoginAsync(_tenant.TenantId!.Value, result.User, ct);
        var requirement = decision.Requirement;
        // Step-up: if the policy demands MFA but the user has no factor (so the normal gate is
        // NotRequired), force enrollment before sign-in. Users with a factor are already challenged.
        if (forceStepUp && requirement == MfaLoginRequirement.NotRequired)
            requirement = MfaLoginRequirement.EnrollmentRequired;

        if (requirement != MfaLoginRequirement.NotRequired)
        {
            _mfaPending.Save(HttpContext, new MfaPendingLogin(
                result.User.Id, result.User.TenantId, result.Session.Id,
                result.User.Email, result.User.EmailVerified, returnUrl));

            return requirement == MfaLoginRequirement.EnrollmentRequired
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

    /// <summary>Ends an impersonation session: revoke it, drop the end-user cookie, return to the admin panel.</summary>
    [HttpPost("stop-impersonation")]
    [Authorize(Policy = AuthPolicies.User)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StopImpersonation(CancellationToken ct)
    {
        var impersonator = User.FindFirstValue(UserClaims.ImpersonatorId);
        var sessionId = User.FindFirstValue(UserClaims.SessionId);
        if (impersonator is not null && Guid.TryParse(sessionId, out var sid)
            && Guid.TryParse(User.FindFirstValue(UserClaims.TenantId), out var tid))
        {
            var actor = new AuditContext(Guid.Parse(impersonator), "user",
                HttpContext.Connection.RemoteIpAddress?.ToString(), CurrentRequest().UserAgent);
            await _impersonation.StopAsync(tid, sid, actor, ct);
        }

        await HttpContext.SignOutAsync(AuthSchemes.User);
        // The admin's own TenantAdmin cookie is still valid.
        return Redirect("/tenantadmin/users");
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
    public IActionResult ForgotPassword(string? returnUrl = null)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Forgot password";
        return View(new ForgotPasswordViewModel { ReturnUrl = returnUrl });
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
        ViewData["ReturnUrl"] = SafeReturnUrl(model.ReturnUrl);
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
    public IActionResult MagicLink(string? returnUrl = null)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Email me a sign-in link";
        return View(new EmailOnlyViewModel { ReturnUrl = returnUrl });
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
        ViewData["ReturnUrl"] = SafeReturnUrl(model.ReturnUrl);
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

    // --- Phone sign-in (WhatsApp OTP or password) ---

    [HttpGet("phone-login")]
    public async Task<IActionResult> PhoneLogin(string? returnUrl = null, CancellationToken ct = default)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        if (!await PhoneAuthEnabledAsync(login: true, ct)) return RedirectToAction(nameof(Login), new { returnUrl = SafeReturnUrl(returnUrl) });
        ViewData["Title"] = "Sign in with phone";
        await PopulateCaptchaAsync(ct);
        return View(new PhoneLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("phone-login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PhoneLogin(PhoneLoginViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        if (!await PhoneAuthEnabledAsync(login: true, ct)) return RedirectToAction(nameof(Login), new { returnUrl = SafeReturnUrl(model.ReturnUrl) });
        ViewData["Title"] = "Sign in with phone";
        await PopulateCaptchaAsync(ct);
        if (!ModelState.IsValid) return View(model);

        var tenantId = _tenant.TenantId!.Value;
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var phone = Authly.Modules.Common.PhoneNumber.Normalize(model.Phone) ?? string.Empty;

        if (!await _blockList.IsIpAllowedAsync(tenantId, ip, ct) || await _blockList.IsIpBlockedAsync(tenantId, ip, ct))
        {
            ModelState.AddModelError(string.Empty, "Sign-in isn't available from your network.");
            return View(model);
        }
        if (await _lockout.IsLockedAsync(tenantId, phone, ct))
        {
            ModelState.AddModelError(string.Empty, "Too many failed attempts. Try again later.");
            return View(model);
        }
        if (!await _screening.VerifyCaptchaAsync(tenantId, CaptchaToken(), ip, ct))
        {
            ModelState.AddModelError(string.Empty, "Please complete the verification challenge.");
            return View(model);
        }

        // Password mode: resolve by verified phone, verify password, then the usual access/MFA gate.
        if (string.Equals(model.Mode, "password", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrEmpty(model.Password))
            {
                ModelState.AddModelError(nameof(model.Password), "Enter your password.");
                return View(model);
            }
            var result = await _auth.AuthenticateByPhoneAsync(tenantId, phone, model.Password, CurrentRequest(), ct);
            if (!result.Succeeded || result.User is null || result.Session is null)
            {
                await _lockout.RecordFailureAsync(tenantId, phone, ct);
                ModelState.AddModelError(string.Empty, "Invalid phone number or password.");
                return View(model);
            }
            await _lockout.ResetAsync(tenantId, phone, ct);
            return await CompleteUserLoginAsync(result.User, result.Session, SafeReturnUrl(model.ReturnUrl),
                onBlocked: () => { ModelState.AddModelError(string.Empty, "Sign-in was blocked by your organization's access policy."); return View(model); }, ct);
        }

        // OTP mode: resolve by verified phone and send a code. Anti-enumeration — same response either way.
        var user = await _users.GetByVerifiedPhoneAsync(tenantId, phone, ct);
        if (user is not null && user.Status == Authly.Core.Enums.UserStatus.Active)
        {
            await _mfa.SendPhoneOtpAsync(tenantId, user, ct);
            _phonePending.Save(HttpContext, new Authly.Web.Infrastructure.Auth.PhonePendingOtp(
                user.Id, tenantId, "login", SafeReturnUrl(model.ReturnUrl)));
        }
        else
        {
            // No pending cookie set — the OTP page will reject any code, but we still show "sent".
            _phonePending.Clear(HttpContext);
        }
        TempData["PhoneOtpSent"] = true;
        return RedirectToAction(nameof(PhoneOtp), new { returnUrl = SafeReturnUrl(model.ReturnUrl) });
    }

    [HttpGet("phone-otp")]
    public async Task<IActionResult> PhoneOtp(string? returnUrl = null, CancellationToken ct = default)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        // Reachable both after a login OTP request and after signup phone verification.
        if (_phonePending.Read(HttpContext) is null && TempData["PhoneOtpSent"] is null)
            return RedirectToAction(nameof(Login));
        ViewData["Title"] = "Enter your code";
        return View(new PhoneOtpViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("phone-otp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PhoneOtp(PhoneOtpViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Enter your code";
        if (!ModelState.IsValid) return View(model);

        var pending = _phonePending.Read(HttpContext);
        if (pending is null)
        {
            ModelState.AddModelError(string.Empty, "This verification has expired. Start again.");
            return View(model);
        }

        var actor = new AuditContext(pending.UserId, "user",
            HttpContext.Connection.RemoteIpAddress?.ToString(), CurrentRequest().UserAgent);
        var ok = await _mfa.VerifyPhoneOtpAsync(pending.TenantId, pending.UserId, model.Code, actor, ct);
        if (!ok)
        {
            ModelState.AddModelError(nameof(model.Code), "That code is invalid or has expired.");
            return View(model);
        }

        var user = await _users.GetByIdAsync(pending.TenantId, pending.UserId, ct);
        if (user is null)
        {
            _phonePending.Clear(HttpContext);
            return RedirectToAction(nameof(Login));
        }

        // Signup verification: just mark the phone verified and continue to sign-in.
        if (pending.Purpose == "signup_verify" && !user.PhoneVerified)
        {
            user.PhoneVerified = true;
            await _users.UpdateAsync(user, ct);
        }

        _phonePending.Clear(HttpContext);

        // OTP is itself a possession factor — sign the user in directly (mirrors magic-link).
        var session = await _auth.StartSessionAsync(user, "phone_otp", CurrentRequest(), ct);
        QueueSuspiciousLoginCheck(user.Id);
        await UserSignIn.SignInAsync(HttpContext, user.Id, user.Email, user.TenantId, session.Id, user.EmailVerified);
        return pending.ReturnUrl is not null ? Redirect(pending.ReturnUrl) : Portal();
    }

    // --- Account recovery ---

    [HttpGet("recover-request")]
    public IActionResult RecoverRequest(string? returnUrl = null)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Recover your account";
        return View(new EmailOnlyViewModel { ReturnUrl = returnUrl });
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
        ViewData["ReturnUrl"] = SafeReturnUrl(model.ReturnUrl);
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

    /// <summary>Whether self-service password sign-up is open for the current tenant.</summary>
    private async Task<bool> SignupAllowedAsync(CancellationToken ct)
        => (await _securitySettings.GetAsync(_tenant.TenantId!.Value, ct)).AllowPasswordSignup;

    /// <summary>Whether phone login / signup is enabled by policy AND WhatsApp + the OTP template are ready.</summary>
    private async Task<bool> PhoneAuthEnabledAsync(bool login, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId!.Value;
        var s = await _securitySettings.GetAsync(tenantId, ct);
        var toggled = login ? s.AllowPhoneLogin : s.AllowPhoneSignup;
        return toggled && await _messaging.IsWhatsAppOtpReadyAsync(tenantId, ct);
    }

    /// <summary>Shared post-credential completion (device record, conditional-access, MFA gate, sign-in)
    /// for a resolved user + session. <paramref name="onBlocked"/> renders the caller's view when a
    /// conditional-access policy blocks the sign-in.</summary>
    private async Task<IActionResult> CompleteUserLoginAsync(Authly.Core.Entities.User user,
        Authly.Core.Entities.Session session, string? returnUrl, Func<IActionResult> onBlocked, CancellationToken ct)
    {
        var tenantId = user.TenantId;
        await _devices.RecordLoginAsync(tenantId, user.Id, CurrentRequest(), ct);
        QueueSuspiciousLoginCheck(user.Id);

        var access = await _conditional.EvaluateAsync(tenantId, user, CurrentRequest(), ct);
        if (access.Action == Authly.Modules.Security.ConditionalAction.Block)
        {
            await _auth.RevokeSessionAsync(session.Id, ct);
            var actor = new AuditContext(user.Id, "user", HttpContext.Connection.RemoteIpAddress?.ToString(), CurrentRequest().UserAgent);
            await _audit.LogAsync("user.login_blocked", actor, tenantId, "user", user.Id,
                result: "blocked", metadata: new { reason = access.Reason }, ct: ct);
            return onBlocked();
        }
        var forceStepUp = access.Action == Authly.Modules.Security.ConditionalAction.RequireMfa;

        var decision = await _mfa.EvaluateLoginAsync(tenantId, user, ct);
        var requirement = decision.Requirement;
        if (forceStepUp && requirement == MfaLoginRequirement.NotRequired)
            requirement = MfaLoginRequirement.EnrollmentRequired;

        if (requirement != MfaLoginRequirement.NotRequired)
        {
            _mfaPending.Save(HttpContext, new MfaPendingLogin(
                user.Id, user.TenantId, session.Id, user.Email, user.EmailVerified, returnUrl));
            return requirement == MfaLoginRequirement.EnrollmentRequired
                ? RedirectToAction(nameof(MfaController.Enroll), "Mfa")
                : RedirectToAction(nameof(MfaController.Challenge), "Mfa");
        }

        await UserSignIn.SignInAsync(HttpContext, user.Id, user.Email, user.TenantId, session.Id, user.EmailVerified);
        return returnUrl is not null ? Redirect(returnUrl) : Portal();
    }

    /// <summary>Sign-up is closed by policy: send the visitor to the login page with a notice.</summary>
    private IActionResult SignupClosed(string? returnUrl)
    {
        TempData["Error"] = "Sign-ups are disabled. Contact your administrator for access.";
        return RedirectToAction(nameof(Login), new { returnUrl = SafeReturnUrl(returnUrl) });
    }

    /// <summary>
    /// Returns <paramref name="url"/> only if it is a safe local redirect target (the OAuth
    /// /connect/authorize request, the portal, etc.); otherwise null. Blocks open-redirects to
    /// off-site URLs that an attacker might smuggle into the ReturnUrl.
    /// </summary>
    private string? SafeReturnUrl(string? url)
        => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : null;

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
