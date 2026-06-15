using System.Text;
using System.Text.Json;
using Authly.Modules.Compliance;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>
/// Portal: data-subject rights (GDPR/DPDP). View consent history, download a full data export,
/// and permanently erase the account. All actions are scoped to the signed-in user.
/// </summary>
[Route("portal/privacy")]
public sealed class PrivacyController : PortalControllerBase
{
    private static readonly JsonSerializerOptions ExportJson = new() { WriteIndented = true };

    private readonly IConsentService _consent;
    private readonly IDataRightsService _dataRights;

    public PrivacyController(IConsentService consent, IDataRightsService dataRights)
    {
        _consent = consent;
        _dataRights = dataRights;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Privacy & data";
        ViewData["Email"] = UserEmail;
        return View(await _consent.ListAsync(TenantId, UserId, ct));
    }

    /// <summary>Download a complete export of the user's data as a JSON file (right of access/portability).</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var export = await _dataRights.ExportAsync(TenantId, UserId, CurrentAudit(), ct);
        if (export is null) return NotFound();

        var json = JsonSerializer.SerializeToUtf8Bytes(export, ExportJson);
        var name = $"authly-data-export-{UserId}.json";
        return File(json, "application/json", name);
    }

    /// <summary>Permanently erase the account. Requires retyping the email to confirm intent.</summary>
    [HttpPost("erase")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Erase(string confirmEmail, CancellationToken ct)
    {
        if (!string.Equals(confirmEmail?.Trim(), UserEmail, StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Type your email exactly to confirm erasure.";
            return RedirectToAction(nameof(Index));
        }

        var erased = await _dataRights.EraseAsync(TenantId, UserId, CurrentAudit(), ct);
        if (!erased)
        {
            TempData["Error"] = "We couldn't complete the erasure. Please contact support.";
            return RedirectToAction(nameof(Index));
        }

        // The account is gone; drop the session cookie and show a final confirmation.
        await HttpContext.SignOutAsync(AuthSchemes.User);
        return RedirectToAction(nameof(Erased));
    }

    [HttpGet("erased")]
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public IActionResult Erased()
    {
        ViewData["Title"] = "Account erased";
        return View();
    }
}
