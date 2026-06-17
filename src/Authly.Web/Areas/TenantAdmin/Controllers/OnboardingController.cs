using Authly.Core.Interfaces;
using Authly.Modules.ApiKeys;
using Authly.Modules.Applications;
using Authly.Modules.Branding;
using Authly.Modules.Tenants;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Guided first-run wizard: create the first application, grab credentials, set branding, and
/// reach a working test login. Each step reuses the same module services as the standalone pages,
/// then marks the tenant onboarded so the dashboard banner disappears.
/// </summary>
[Route("tenantadmin/onboarding")]
public sealed class OnboardingController : TenantAdminControllerBase
{
    private readonly IApplicationService _apps;
    private readonly IApiKeyService _apiKeys;
    private readonly IBrandingService _branding;
    private readonly ITenantService _tenants;

    public OnboardingController(
        IApplicationService apps, IApiKeyService apiKeys, IBrandingService branding,
        ITenantService tenants, ITenantContext tenant) : base(tenant)
    {
        _apps = apps;
        _apiKeys = apiKeys;
        _branding = branding;
        _tenants = tenants;
    }

    // Step 1 — create the first application.
    [RequireOperatorPermission("project.read")]
    [HttpGet("")]
    public IActionResult Index()
    {
        ViewData["Title"] = "Get started";
        ViewBag.Step = 1;
        return View(new CreateApplicationViewModel());
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("app")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateApp(CreateApplicationViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Get started";
        ViewBag.Step = 1;
        if (!ModelState.IsValid) return View(nameof(Index), model);

        var redirectUris = (model.RedirectUris ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var postLogoutUris = (model.PostLogoutRedirectUris ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var scopes = (model.Scopes ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var result = await _apps.CreateAsync(TenantId,
            new CreateApplicationRequest(model.Name, model.Type, redirectUris, scopes, postLogoutUris), CurrentAudit(), ct);

        TempData["NewClientId"] = result.Application.ClientId;
        if (result.ClientSecret is not null)
            TempData["NewClientSecret"] = result.ClientSecret;

        return RedirectToAction(nameof(Keys), new { appId = result.Application.Id });
    }

    // Step 2 — show credentials and optionally mint a backend API key.
    [RequireOperatorPermission("project.read")]
    [HttpGet("keys")]
    public async Task<IActionResult> Keys(Guid appId, CancellationToken ct)
    {
        var app = await _apps.GetAsync(TenantId, appId, ct);
        if (app is null) return RedirectToAction(nameof(Index));

        ViewData["Title"] = "Get started";
        ViewBag.Step = 2;
        return View(app);
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("api-key")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateApiKey(Guid appId, string name, CancellationToken ct)
    {
        var result = await _apiKeys.CreateAsync(TenantId,
            new CreateApiKeyRequest(string.IsNullOrWhiteSpace(name) ? "Server key" : name.Trim(),
                new[] { "openid", "profile", "email" }), CurrentAudit(), ct);

        TempData["NewApiKey"] = result.RawKey;
        return RedirectToAction(nameof(Keys), new { appId });
    }

    // Step 3 — branding.
    [RequireOperatorPermission("project.read")]
    [HttpGet("branding")]
    public async Task<IActionResult> Branding(CancellationToken ct)
    {
        ViewData["Title"] = "Get started";
        ViewBag.Step = 3;
        var current = await _branding.GetAsync(TenantId, ct);
        return View(new OnboardingBrandingViewModel
        {
            PrimaryColor = current.PrimaryColor,
            LogoUrl = current.LogoUrl
        });
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("branding")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Branding(OnboardingBrandingViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Get started";
        ViewBag.Step = 3;
        if (!ModelState.IsValid) return View(model);

        var current = await _branding.GetAsync(TenantId, ct);
        var input = BrandingInput.From(current);
        input.LogoUrl = string.IsNullOrWhiteSpace(model.LogoUrl) ? null : model.LogoUrl.Trim();
        input.PrimaryColor = model.PrimaryColor;
        await _branding.SaveAsync(TenantId, input, CurrentAudit(), ct);

        return RedirectToAction(nameof(TestLogin));
    }

    // Step 4 — test login pointer + finish.
    [RequireOperatorPermission("project.read")]
    [HttpGet("test")]
    public IActionResult TestLogin()
    {
        ViewData["Title"] = "Get started";
        ViewBag.Step = 4;
        return View();
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("finish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finish(CancellationToken ct)
    {
        await _tenants.SetOnboardedAsync(TenantId, CurrentAudit(), ct);
        TempData["Success"] = "You're all set. Welcome to Authly.";
        return RedirectToAction("Index", "Applications");
    }
}
