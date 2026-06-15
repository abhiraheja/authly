using Authly.Core.Interfaces;
using Authly.Modules.ApiKeys;
using Authly.Web.Areas.TenantAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin management of Management-API keys: mint (shown once), list, revoke.</summary>
[Route("tenantadmin/api-keys")]
public sealed class ApiKeysController : TenantAdminControllerBase
{
    private readonly IApiKeyService _keys;

    public ApiKeysController(IApiKeyService keys, ITenantContext tenant) : base(tenant) => _keys = keys;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "API keys";
        return View(await _keys.ListAsync(TenantId, ct));
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New API key";
        return View(new CreateApiKeyViewModel());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApiKeyViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "New API key";
        if (!ModelState.IsValid) return View(model);

        var scopes = (model.Scopes ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var result = await _keys.CreateAsync(TenantId,
            new CreateApiKeyRequest(model.Name, scopes), CurrentAudit(), ct);

        // Surface the raw key one time on the list page.
        TempData["NewApiKey"] = result.RawKey;
        TempData["Success"] = "API key created. Copy it now — it won't be shown again.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        try
        {
            await _keys.RevokeAsync(TenantId, id, CurrentAudit(), ct);
            TempData["Success"] = "API key revoked.";
        }
        catch (ApiKeyNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
