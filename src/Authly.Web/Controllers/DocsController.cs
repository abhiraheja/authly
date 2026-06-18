using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>Public developer documentation hub (mirrors docs/developer/Quickstart.md + Tenant-Onboarding-Guide.md).</summary>
[AllowAnonymous]
[Route("docs")]
public sealed class DocsController : Controller
{
    /// <summary>Populates ViewBag with this deployment's live endpoint URLs for every docs page.</summary>
    private void PopulateEndpoints()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        ViewBag.BaseUrl = baseUrl;
        ViewBag.Discovery = $"{baseUrl}/.well-known/openid-configuration";
        ViewBag.Authorize = $"{baseUrl}/connect/authorize";
        ViewBag.Token = $"{baseUrl}/connect/token";
        ViewBag.UserInfo = $"{baseUrl}/connect/userinfo";
        ViewBag.Introspect = $"{baseUrl}/connect/introspect";
        ViewBag.Revoke = $"{baseUrl}/connect/revoke";
        ViewBag.Logout = $"{baseUrl}/connect/logout";
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        PopulateEndpoints();
        return View();
    }

    [HttpGet("quickstart")]
    public IActionResult Quickstart()
    {
        PopulateEndpoints();
        return View();
    }

    [HttpGet("oauth")]
    public IActionResult OAuth()
    {
        PopulateEndpoints();
        return View();
    }

    [HttpGet("api")]
    public IActionResult Api()
    {
        PopulateEndpoints();
        return View();
    }

    [HttpGet("webhooks")]
    public IActionResult Webhooks()
    {
        PopulateEndpoints();
        return View();
    }

    [HttpGet("admin")]
    public IActionResult Admin()
    {
        PopulateEndpoints();
        return View();
    }

    [HttpGet("portal")]
    public IActionResult Portal()
    {
        PopulateEndpoints();
        return View();
    }

    [HttpGet("self-host")]
    public IActionResult SelfHost()
    {
        PopulateEndpoints();
        return View();
    }

    [HttpGet("production")]
    public IActionResult Production()
    {
        PopulateEndpoints();
        return View();
    }
}
