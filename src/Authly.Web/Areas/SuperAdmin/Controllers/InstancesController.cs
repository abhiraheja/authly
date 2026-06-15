using Authly.Modules.Compliance;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.SuperAdmin.Controllers;

/// <summary>
/// Platform view of registered self-hosted instances (§4.14). Operators register an instance to
/// issue a one-time sync key and monitor the aggregate telemetry each instance pushes.
/// </summary>
public sealed class InstancesController : SuperAdminControllerBase
{
    private readonly ISelfHostSyncService _sync;

    public InstancesController(ISelfHostSyncService sync) => _sync = sync;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Self-hosted instances";
        // The raw key (shown once) survives a redirect via TempData.
        ViewData["NewKey"] = TempData["NewKey"];
        ViewData["NewKeyName"] = TempData["NewKeyName"];
        return View(await _sync.ListAsync(ct));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string? name, CancellationToken ct)
    {
        var reg = await _sync.RegisterAsync(ownerTenantId: null, name: name, CurrentAudit(), ct);
        // Shown exactly once — never stored or retrievable again.
        TempData["NewKey"] = reg.RawSyncKey;
        TempData["NewKeyName"] = string.IsNullOrWhiteSpace(name) ? "(unnamed)" : name.Trim();
        return RedirectToAction(nameof(Index));
    }
}
