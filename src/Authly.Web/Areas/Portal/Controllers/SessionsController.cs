using Authly.Modules.Account;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal: list active sessions and revoke them (Phase 10).</summary>
[Route("portal/sessions")]
public sealed class SessionsController : PortalControllerBase
{
    private readonly IAccountSelfService _account;

    public SessionsController(IAccountSelfService account) => _account = account;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Active sessions";
        ViewData["CurrentSessionId"] = CurrentSessionId;
        var sessions = await _account.ListSessionsAsync(TenantId, UserId, ct);
        return View(sessions);
    }

    [HttpPost("{id:guid}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        await _account.RevokeSessionAsync(TenantId, UserId, id, CurrentAudit(), ct);
        TempData["Success"] = id == CurrentSessionId ? "This session was signed out." : "Session revoked.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("revoke-others")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeOthers(CancellationToken ct)
    {
        var count = await _account.RevokeOtherSessionsAsync(TenantId, UserId, CurrentSessionId, CurrentAudit(), ct);
        TempData["Success"] = count > 0 ? $"Signed out {count} other session(s)." : "No other active sessions.";
        return RedirectToAction(nameof(Index));
    }
}
