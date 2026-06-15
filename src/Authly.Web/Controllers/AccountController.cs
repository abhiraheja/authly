using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Modules.Mfa;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.Mfa;
using Authly.Web.Models;
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

    public AccountController(IAuthService auth, ITenantContext tenant, IMfaService mfa, MfaPendingStore mfaPending,
        Authly.Modules.Social.ISocialLoginService social)
    {
        _auth = auth;
        _tenant = tenant;
        _mfa = mfa;
        _mfaPending = mfaPending;
        _social = social;
    }

    // --- Registration ---

    [HttpGet("register")]
    public IActionResult Register()
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Create your account";
        return View(new RegisterViewModel());
    }

    [HttpPost("register")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Create your account";
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _auth.RegisterAsync(_tenant.TenantId!.Value,
                new RegisterRequest(model.Email, model.Password, model.FirstName, model.LastName),
                CurrentRequest(), ct);
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
        return View(new UserLoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(UserLoginViewModel model, CancellationToken ct)
    {
        if (RequireTenant() is { } noTenant) return noTenant;
        ViewData["Title"] = "Sign in";
        if (!ModelState.IsValid) return View(model);

        var result = await _auth.AuthenticateAsync(_tenant.TenantId!.Value, model.Email, model.Password,
            CurrentRequest(), ct);

        if (!result.Succeeded || result.User is null || result.Session is null)
        {
            // Same message for bad credentials and suspended accounts (no account-state leak).
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

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

        var ok = await _auth.ResetPasswordAsync(_tenant.TenantId!.Value, model.Token, model.NewPassword, ct);
        if (!ok)
        {
            ModelState.AddModelError(string.Empty, "This reset link is invalid or has expired. Request a new one.");
            return View(model);
        }

        TempData["Success"] = "Your password has been reset. Please sign in.";
        return RedirectToAction(nameof(Login));
    }

    // --- helpers ---

    /// <summary>Returns a view result when no tenant is resolved; null when a tenant is in scope.</summary>
    private IActionResult? RequireTenant()
        => _tenant.HasTenant ? null : View("TenantRequired");

    private RequestInfo CurrentRequest()
        => new(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
