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
            BrandName = b.BrandName,
            PrimaryColor = b.PrimaryColor,
            ButtonTextColor = b.ButtonTextColor,
            FontFamily = b.FontFamily,
            DarkMode = b.DarkMode,
            Layout = b.Layout,
            Background = b.Background,
            GradientFrom = b.GradientFrom,
            GradientTo = b.GradientTo,
            BackgroundImageUrl = b.BackgroundImageUrl,
            BackgroundFit = b.BackgroundFit,
            BackgroundPosition = b.BackgroundPosition,
            OverlayOpacity = b.OverlayOpacity,
            Heading = b.Heading,
            Subtitle = b.Subtitle,
            HeadingSize = b.HeadingSize,
            Tagline = b.Tagline,
            FeatureBulletsText = string.Join("\n", b.FeatureBullets),
            FooterText = b.FooterText,
            CardStyle = b.CardStyle,
            CardShadow = b.CardShadow,
            CornerRadius = b.CornerRadius,
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
                BrandName = model.BrandName,
                PrimaryColor = model.PrimaryColor,
                ButtonTextColor = model.ButtonTextColor,
                FontFamily = model.FontFamily,
                DarkMode = model.DarkMode,
                Layout = model.Layout,
                Background = model.Background,
                GradientFrom = model.GradientFrom,
                GradientTo = model.GradientTo,
                BackgroundImageUrl = model.BackgroundImageUrl,
                BackgroundFit = model.BackgroundFit,
                BackgroundPosition = model.BackgroundPosition,
                OverlayOpacity = model.OverlayOpacity,
                Heading = model.Heading,
                Subtitle = model.Subtitle,
                HeadingSize = model.HeadingSize,
                Tagline = model.Tagline,
                FeatureBullets = (model.FeatureBulletsText ?? "")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList(),
                FooterText = model.FooterText,
                CardStyle = model.CardStyle,
                CardShadow = model.CardShadow,
                CornerRadius = model.CornerRadius
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
