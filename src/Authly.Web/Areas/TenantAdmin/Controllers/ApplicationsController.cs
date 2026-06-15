using Authly.Core.Interfaces;
using Authly.Modules.Applications;
using Authly.Web.Areas.TenantAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin management of OAuth clients (applications): list, create, rotate secret, delete.</summary>
[Route("tenantadmin/applications")]
public sealed class ApplicationsController : TenantAdminControllerBase
{
    private readonly IApplicationService _apps;

    public ApplicationsController(IApplicationService apps, ITenantContext tenant) : base(tenant)
        => _apps = apps;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Applications";
        return View(await _apps.ListAsync(TenantId, ct));
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        ViewData["Title"] = "New application";
        return View(new CreateApplicationViewModel());
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApplicationViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "New application";
        if (!ModelState.IsValid) return View(model);

        var redirectUris = (model.RedirectUris ?? string.Empty)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        var scopes = (model.Scopes ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        var result = await _apps.CreateAsync(TenantId,
            new CreateApplicationRequest(model.Name, model.Type, redirectUris, scopes),
            CurrentAudit(), ct);

        // Surface the client id and (one-time) secret on the details page.
        TempData["NewClientId"] = result.Application.ClientId;
        if (result.ClientSecret is not null)
            TempData["NewClientSecret"] = result.ClientSecret;

        return RedirectToAction(nameof(Details), new { id = result.Application.Id });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var app = await _apps.GetAsync(TenantId, id, ct);
        if (app is null) return NotFound();

        ViewData["Title"] = app.Name;
        ViewData["Secrets"] = await _apps.ListSecretsAsync(TenantId, id, ct);
        return View(app);
    }

    [HttpPost("{id:guid}/rotate-secret")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateSecret(Guid id, CancellationToken ct)
    {
        try
        {
            var secret = await _apps.RotateSecretAsync(TenantId, id, CurrentAudit(), ct);
            TempData["NewClientSecret"] = secret;
            TempData["Success"] = "A new client secret was generated. Copy it now — it won't be shown again.";
        }
        catch (PublicClientHasNoSecretException)
        {
            TempData["Error"] = "Public clients use PKCE and don't have a client secret.";
        }
        catch (ApplicationNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _apps.DeleteAsync(TenantId, id, CurrentAudit(), ct);
            TempData["Success"] = "Application deleted.";
        }
        catch (ApplicationNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Index));
    }
}
