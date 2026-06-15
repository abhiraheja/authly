using Authly.Modules.Devices;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal: a user's known devices — trust, rename, and forget (Phase 2).</summary>
[Route("portal/devices")]
public sealed class DevicesController : PortalControllerBase
{
    private readonly IDeviceService _devices;

    public DevicesController(IDeviceService devices) => _devices = devices;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Devices";
        return View(await _devices.ListAsync(TenantId, UserId, ct));
    }

    [HttpPost("{id:guid}/trust")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Trust(Guid id, bool trusted, CancellationToken ct)
    {
        try
        {
            await _devices.SetTrustedAsync(TenantId, UserId, id, trusted, CurrentAudit(), ct);
            TempData["Success"] = trusted ? "Device trusted." : "Device is no longer trusted.";
        }
        catch (KeyNotFoundException) { TempData["Error"] = "That device no longer exists."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/rename")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Rename(Guid id, string label, CancellationToken ct)
    {
        try
        {
            await _devices.RenameAsync(TenantId, UserId, id, label, CurrentAudit(), ct);
            TempData["Success"] = "Device renamed.";
        }
        catch (ArgumentException) { TempData["Error"] = "Device name must be 1–80 characters."; }
        catch (KeyNotFoundException) { TempData["Error"] = "That device no longer exists."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/forget")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Forget(Guid id, CancellationToken ct)
    {
        try
        {
            await _devices.ForgetAsync(TenantId, UserId, id, CurrentAudit(), ct);
            TempData["Success"] = "Device forgotten.";
        }
        catch (KeyNotFoundException) { TempData["Error"] = "That device no longer exists."; }
        return RedirectToAction(nameof(Index));
    }
}
