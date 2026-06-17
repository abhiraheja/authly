using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>Public developer documentation landing page (mirrors docs/developer/Quickstart.md).</summary>
[AllowAnonymous]
[Route("docs")]
public sealed class DocsController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        ViewBag.Discovery = $"{baseUrl}/.well-known/openid-configuration";
        ViewBag.Authorize = $"{baseUrl}/connect/authorize";
        ViewBag.Token = $"{baseUrl}/connect/token";
        ViewBag.UserInfo = $"{baseUrl}/connect/userinfo";
        ViewBag.Logout = $"{baseUrl}/connect/logout";
        return View();
    }
}
