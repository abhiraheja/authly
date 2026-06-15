using Authly.Core.Interfaces;
using Authly.Modules.Social;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin configuration of social / OAuth2 login providers (§4.3 / Phase 8).</summary>
[Route("tenantadmin/social")]
public sealed class SocialProvidersController : TenantAdminControllerBase
{
    private readonly ISocialLoginService _social;

    public SocialProvidersController(ISocialLoginService social, ITenantContext tenant) : base(tenant)
        => _social = social;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Social login";
        return View(await _social.ListProvidersAsync(TenantId, ct));
    }

    [HttpGet("edit")]
    public async Task<IActionResult> Edit(string provider, Guid? id, CancellationToken ct)
    {
        ViewData["Title"] = "Social provider";
        var input = new SocialProviderInput { Provider = provider };
        if (id is { } pid && await _social.GetProviderAsync(TenantId, pid, ct) is { } existing)
        {
            input.Id = existing.Id;
            input.Provider = existing.Provider;
            input.ClientId = existing.ClientId;
            input.Scopes = existing.Scopes;
            input.IsActive = existing.IsActive;
            input.AuthorizationEndpoint = existing.AuthorizationEndpoint;
            input.TokenEndpoint = existing.TokenEndpoint;
            input.UserInfoEndpoint = existing.UserInfoEndpoint;
            // ClientSecret is write-only — never sent back to the form.
        }
        return View(input);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(SocialProviderInput input, CancellationToken ct)
    {
        ViewData["Title"] = "Social provider";
        try
        {
            await _social.SaveProviderAsync(TenantId, input, CurrentAudit(), ct);
            TempData["Success"] = $"{input.Provider} provider saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (SocialProviderConfigInvalidException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(nameof(Edit), input);
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _social.DeleteProviderAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Provider removed.";
        return RedirectToAction(nameof(Index));
    }
}
