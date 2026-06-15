using Authly.Core.Interfaces;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Web.Areas.TenantAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Per-tenant sandbox: a safe place for an admin to verify the auth setup. The test login runs a
/// real credential check against this tenant's user store (scoped to <see cref="TenantId"/>) but
/// never issues an admin/user cookie — it only reports the outcome. Also surfaces the OAuth/OIDC
/// endpoints a developer needs to integrate.
/// </summary>
[Route("tenantadmin/sandbox")]
public sealed class SandboxController : TenantAdminControllerBase
{
    private readonly IAuthService _auth;

    public SandboxController(IAuthService auth, ITenantContext tenant) : base(tenant) => _auth = auth;

    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Sandbox";
        PopulateEndpoints();
        return View(new SandboxLoginViewModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SandboxLoginViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Sandbox";
        PopulateEndpoints();
        if (!ModelState.IsValid) return View(model);

        var info = new RequestInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);

        var result = await _auth.AuthenticateAsync(TenantId, model.Email, model.Password, info, ct);

        ViewBag.Outcome = result.Outcome.ToString();
        ViewBag.Succeeded = result.Succeeded;
        if (result.Succeeded && result.User is not null)
        {
            ViewBag.UserId = result.User.Id;
            ViewBag.UserEmail = result.User.Email;
            ViewBag.EmailVerified = result.User.EmailVerified;
            ViewBag.UserStatus = result.User.Status.ToString();
            ViewBag.SessionId = result.Session?.Id;
        }

        // Don't echo the submitted password back into the form.
        model.Password = string.Empty;
        return View(model);
    }

    private void PopulateEndpoints()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        ViewBag.Discovery = $"{baseUrl}/.well-known/openid-configuration";
        ViewBag.Authorize = $"{baseUrl}/connect/authorize";
        ViewBag.Token = $"{baseUrl}/connect/token";
        ViewBag.UserInfo = $"{baseUrl}/connect/userinfo";
    }
}
