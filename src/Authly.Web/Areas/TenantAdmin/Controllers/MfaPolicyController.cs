using Authly.Core.Interfaces;
using Authly.Modules.Mfa;
using Authly.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin configuration of the workspace's MFA policy (§4.4 / Phase 6).</summary>
[Route("tenantadmin/mfa")]
public sealed class MfaPolicyController : TenantAdminControllerBase
{
    private readonly IMfaService _mfa;

    public MfaPolicyController(IMfaService mfa, ITenantContext tenant) : base(tenant)
        => _mfa = mfa;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Multi-factor authentication";
        var settings = await _mfa.GetPolicyAsync(TenantId, ct);
        return View(new MfaPolicyViewModel
        {
            Policy = settings.Policy,
            AllowTotp = settings.AllowTotp,
            AllowEmailOtp = settings.AllowEmailOtp
        });
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(MfaPolicyViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Multi-factor authentication";
        if (!model.AllowTotp && !model.AllowEmailOtp)
        {
            ModelState.AddModelError(string.Empty, "Enable at least one MFA method.");
            return View(model);
        }

        await _mfa.SetPolicyAsync(TenantId, new TenantMfaSettings
        {
            Policy = model.Policy,
            AllowTotp = model.AllowTotp,
            AllowEmailOtp = model.AllowEmailOtp
        }, CurrentAudit(), ct);

        TempData["Success"] = "MFA policy updated.";
        return RedirectToAction(nameof(Index));
    }
}
