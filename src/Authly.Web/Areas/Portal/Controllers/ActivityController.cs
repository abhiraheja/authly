using Authly.Modules.Account;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal: the signed-in user's login history (Phase 10).</summary>
[Route("portal/activity")]
public sealed class ActivityController : PortalControllerBase
{
    private readonly IAccountSelfService _account;

    public ActivityController(IAccountSelfService account) => _account = account;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Login activity";
        var history = await _account.ListLoginHistoryAsync(TenantId, UserId, 50, ct);
        return View(history);
    }
}
