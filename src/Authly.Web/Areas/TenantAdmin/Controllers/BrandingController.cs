using Authly.Core.Interfaces;
using Authly.Modules.Branding;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Tenant-admin configuration of hosted-page branding (logo, colors, fonts, layout, dark mode)
/// and the custom auth domain (Phase 10).
/// </summary>
[Route("tenantadmin/branding")]
public sealed class BrandingController : TenantAdminControllerBase
{
    private readonly IBrandingService _branding;

    public BrandingController(IBrandingService branding, ITenantContext tenant) : base(tenant)
        => _branding = branding;

    [RequireOperatorPermission("project.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Branding";
        var b = await _branding.GetAsync(TenantId, ct);
        return View(new BrandingViewModel
        {
            LogoUrl = b.LogoUrl,
            PrimaryColor = b.PrimaryColor,
            ButtonTextColor = b.ButtonTextColor,
            FontFamily = b.FontFamily,
            Layout = b.Layout,
            DarkMode = b.DarkMode,
            Tagline = b.Tagline,
            CustomDomain = await _branding.GetCustomDomainAsync(TenantId, ct)
        });
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BrandingViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Branding";
        if (!ModelState.IsValid) return View(model);

        try
        {
            await _branding.SaveAsync(TenantId, new BrandingInput
            {
                LogoUrl = model.LogoUrl,
                PrimaryColor = model.PrimaryColor,
                ButtonTextColor = model.ButtonTextColor,
                FontFamily = model.FontFamily,
                Layout = model.Layout,
                DarkMode = model.DarkMode,
                Tagline = model.Tagline
            }, CurrentAudit(), ct);

            await _branding.SetCustomDomainAsync(TenantId, model.CustomDomain, CurrentAudit(), ct);
        }
        catch (BrandingConfigInvalidException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(model);
        }

        TempData["Success"] = "Branding saved.";
        return RedirectToAction(nameof(Index));
    }
}
