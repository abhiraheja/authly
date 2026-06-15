using Authly.Core.Interfaces;
using Authly.Modules.Hooks;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Tenant-admin configuration of pipeline hooks (§4.12 / Phase 9): per-stage synchronous outbound
/// hooks with timeout + on-failure (continue/block).
/// </summary>
[Route("tenantadmin/hooks")]
public sealed class PipelineHooksController : TenantAdminControllerBase
{
    private readonly IPipelineHookService _hooks;

    public PipelineHooksController(IPipelineHookService hooks, ITenantContext tenant) : base(tenant)
        => _hooks = hooks;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Pipeline hooks";
        return View(await _hooks.ListAsync(TenantId, ct));
    }

    [HttpGet("edit")]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        ViewData["Title"] = "Pipeline hook";
        var input = new PipelineHookInput();
        if (id is { } hid && await _hooks.GetAsync(TenantId, hid, ct) is { } existing)
        {
            input.Id = existing.Id;
            input.Stage = existing.Stage;
            input.Url = existing.Url;
            input.TimeoutMs = existing.TimeoutMs;
            input.OnFailure = existing.OnFailure;
            input.IsActive = existing.IsActive;
            // Secret is write-only.
        }
        return View(input);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PipelineHookInput input, CancellationToken ct)
    {
        ViewData["Title"] = "Pipeline hook";
        try
        {
            await _hooks.SaveAsync(TenantId, input, CurrentAudit(), ct);
            TempData["Success"] = "Pipeline hook saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (PipelineHookConfigInvalidException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(nameof(Edit), input);
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _hooks.DeleteAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Pipeline hook removed.";
        return RedirectToAction(nameof(Index));
    }
}
