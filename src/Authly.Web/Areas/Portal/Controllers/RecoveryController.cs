using Authly.Core.Enums;
using Authly.Modules.AdvancedAuth;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal: manage account-recovery contacts (Phase 11).</summary>
[Route("portal/recovery")]
public sealed class RecoveryController : PortalControllerBase
{
    private readonly IRecoveryService _recovery;

    public RecoveryController(IRecoveryService recovery) => _recovery = recovery;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Account recovery";
        return View(await _recovery.ListContactsAsync(TenantId, UserId, ct));
    }

    [HttpPost("add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(ContactType type, string value, CancellationToken ct)
    {
        try
        {
            await _recovery.AddContactAsync(TenantId, UserId, type, value, CurrentAudit(), ct);
            TempData["Success"] = "Recovery contact added.";
        }
        catch (AdvancedAuthException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        await _recovery.RemoveContactAsync(TenantId, UserId, id, CurrentAudit(), ct);
        TempData["Success"] = "Recovery contact removed.";
        return RedirectToAction(nameof(Index));
    }
}
