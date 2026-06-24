using Authly.Core.Interfaces;
using Authly.Core.OAuth;
using Authly.Modules.Common;
using Authly.Modules.Social;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.Social;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// External / social login (account/external). Drives the OAuth2 authorization-code flow: kicks
/// off at <c>/{provider}</c> and returns at <c>/{provider}/callback</c>. The user is signed into
/// the end-user cookie only after the provider handshake resolves to a tenant user.
/// </summary>
[Route("account/external")]
public sealed class SocialController : Controller
{
    private readonly ISocialLoginService _social;
    private readonly ITenantContext _tenant;
    private readonly SocialStateProtector _state;
    private readonly Authly.Modules.Security.ISecuritySettingsService _securitySettings;

    public SocialController(ISocialLoginService social, ITenantContext tenant, SocialStateProtector state,
        Authly.Modules.Security.ISecuritySettingsService securitySettings)
    {
        _social = social;
        _tenant = tenant;
        _state = state;
        _securitySettings = securitySettings;
    }

    [HttpGet("{provider}")]
    public async Task<IActionResult> Start(string provider, string? returnUrl, CancellationToken ct)
    {
        if (!_tenant.HasTenant) return RedirectToLogin();

        var tenantId = _tenant.TenantId!.Value;
        if (!(await _securitySettings.GetAsync(tenantId, ct)).AllowSocialLogin)
            return RedirectToLogin("That sign-in option isn't available.");
        var redirectUri = Url.Action(nameof(Callback), "Social", new { provider }, Request.Scheme)!;
        var safeReturn = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null;
        var state = _state.Protect(new SocialState(tenantId, provider, redirectUri, safeReturn));

        try
        {
            var url = await _social.BuildAuthorizationUrlAsync(tenantId, provider, redirectUri, state, ct);
            return Redirect(url);
        }
        catch (SocialProviderNotConfiguredException)
        {
            return RedirectToLogin("That sign-in option isn't available.");
        }
        catch (SocialProviderConfigInvalidException)
        {
            return RedirectToLogin("That sign-in option is misconfigured.");
        }
    }

    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(string provider, string? code, string? state, string? error, CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(error) || string.IsNullOrEmpty(code) || string.IsNullOrEmpty(state))
            return RedirectToLogin("Sign-in was cancelled or failed.");

        var parsed = _state.Read(state);
        if (parsed is null || !string.Equals(parsed.Provider, provider, StringComparison.Ordinal))
            return RedirectToLogin("Your sign-in request expired. Please try again.");

        // The state's tenant must match the tenant resolved for this host (no cross-tenant carry-over).
        if (!_tenant.HasTenant || _tenant.TenantId != parsed.TenantId)
            return RedirectToLogin("Your sign-in request expired. Please try again.");

        try
        {
            var result = await _social.CompleteLoginAsync(parsed.TenantId, provider, code, parsed.RedirectUri, CurrentRequest(), ct);
            await UserSignIn.SignInAsync(HttpContext, result.User.Id, result.User.Email,
                result.User.TenantId, result.Session.Id, result.User.EmailVerified);

            return parsed.ReturnUrl is not null && Url.IsLocalUrl(parsed.ReturnUrl)
                ? Redirect(parsed.ReturnUrl)
                : RedirectToAction(nameof(AccountController.Index), "Account");
        }
        catch (SocialProfileMissingEmailException)
        {
            return RedirectToLogin("That account didn't share a verified email, so we couldn't sign you in.");
        }
        catch (SocialSignupDisabledException)
        {
            return RedirectToLogin($"No account exists for this {provider} identity, and sign-ups are disabled. Contact your administrator for access.");
        }
        catch (Exception ex) when (ex is SocialAuthException or SocialProviderConfigInvalidException or SocialProviderNotConfiguredException)
        {
            return RedirectToLogin("We couldn't complete sign-in with that provider. Please try again.");
        }
    }

    private IActionResult RedirectToLogin(string? error = null)
    {
        if (error is not null) TempData["Error"] = error;
        return RedirectToAction(nameof(AccountController.Login), "Account");
    }

    private RequestInfo CurrentRequest()
        => new(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
