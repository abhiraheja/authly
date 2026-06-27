using Authly.Modules.Policies;
using Authly.Web.Areas.Portal.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>
/// Portal: the signed-in user's policy decision history — what they've accepted, skipped or declined,
/// and at which version. Read-only companion to the sign-in consent wall.
/// </summary>
[Route("portal/consents")]
public sealed class ConsentsController : PortalControllerBase
{
    private readonly IPolicyService _policies;

    public ConsentsController(IPolicyService policies) => _policies = policies;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Policies & consents";

        var decisions = await _policies.ListUserDecisionsAsync(TenantId, UserId, ct);
        var titles = (await _policies.ListAsync(TenantId, ct)).ToDictionary(p => p.Id, p => p.Title);

        var rows = decisions
            .Select(d => new ConsentHistoryRow(
                titles.TryGetValue(d.PolicyId, out var t) ? t : "(removed policy)",
                d.Decision, d.Version, d.DecidedAt))
            .ToList();

        return View(rows);
    }
}
