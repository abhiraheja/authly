using System.Text.Json;
using Authly.Core.Interfaces;
using Authly.Modules.AdvancedAuth;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.WebAuthn;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// Passwordless passkey (WebAuthn) sign-in. The browser drives the ceremony: it POSTs the email to
/// get assertion options, calls <c>navigator.credentials.get</c>, then POSTs the result here to be
/// verified. The user is signed into the end-user cookie only on a valid assertion. Tenant-scoped.
/// </summary>
[Route("account/passkey")]
public sealed class PasskeyController : Controller
{
    private const string Purpose = "login";

    private readonly IPasskeyService _passkeys;
    private readonly IUserRepository _users;
    private readonly IAuthService _auth;
    private readonly ITenantContext _tenant;
    private readonly WebAuthnChallengeStore _store;

    public PasskeyController(IPasskeyService passkeys, IUserRepository users, IAuthService auth,
        ITenantContext tenant, WebAuthnChallengeStore store)
    {
        _passkeys = passkeys;
        _users = users;
        _auth = auth;
        _tenant = tenant;
        _store = store;
    }

    [HttpPost("options")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Options([FromForm] string email, CancellationToken ct)
    {
        if (!_tenant.HasTenant) return BadRequest(new { error = "no_tenant" });

        var tenantId = _tenant.TenantId!.Value;
        var user = await _users.GetByEmailAsync(tenantId, (email ?? "").Trim().ToLowerInvariant(), ct);
        if (user is null) return BadRequest(new { error = "no_passkey" });

        var challenge = await _passkeys.BeginLoginAsync(tenantId, user.Id, ct);
        if (challenge is null) return BadRequest(new { error = "no_passkey" });

        _store.Save(HttpContext, new WebAuthnPending(Purpose, tenantId, user.Id, challenge.State));
        return Content(challenge.OptionsJson, "application/json");
    }

    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string? returnUrl, CancellationToken ct)
    {
        if (!_tenant.HasTenant) return BadRequest(new { error = "no_tenant" });

        var pending = _store.Read(HttpContext);
        if (pending is null || pending.Purpose != Purpose || pending.TenantId != _tenant.TenantId)
            return BadRequest(new { error = "expired" });
        _store.Clear(HttpContext);

        string body;
        using (var reader = new StreamReader(Request.Body))
            body = await reader.ReadToEndAsync(ct);

        var user = await _passkeys.CompleteLoginAsync(pending.TenantId, pending.UserId, pending.State, body, ct);
        if (user is null) return BadRequest(new { error = "verification_failed" });

        var session = await _auth.StartSessionAsync(user, "passkey", CurrentRequest(), ct);
        await UserSignIn.SignInAsync(HttpContext, user.Id, user.Email, user.TenantId, session.Id, user.EmailVerified);

        // Resume the original flow (e.g. the OAuth /connect/authorize request) when it's a safe
        // local URL; otherwise land on the portal.
        var redirect = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Action("Index", "Profile", new { area = "Portal" });
        return Ok(new { redirect });
    }

    private RequestInfo CurrentRequest()
        => new(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
