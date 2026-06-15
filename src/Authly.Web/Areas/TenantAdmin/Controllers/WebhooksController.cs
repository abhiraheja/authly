using Authly.Core.Events;
using Authly.Core.Interfaces;
using Authly.Modules.Webhooks;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Tenant-admin configuration of outbound webhooks (§4.12 / Phase 9): endpoints + event
/// subscriptions, the delivery log with manual retry, and a test-in-dashboard fire.
/// </summary>
[Route("tenantadmin/webhooks")]
public sealed class WebhooksController : TenantAdminControllerBase
{
    private readonly IWebhookService _webhooks;

    public WebhooksController(IWebhookService webhooks, ITenantContext tenant) : base(tenant)
        => _webhooks = webhooks;

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Webhooks";
        ViewData["Endpoints"] = await _webhooks.ListEndpointsAsync(TenantId, ct);
        ViewData["Deliveries"] = await _webhooks.ListRecentDeliveriesAsync(TenantId, 50, ct);
        return View();
    }

    [HttpGet("edit")]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        ViewData["Title"] = "Webhook endpoint";
        ViewData["Catalog"] = EventCatalog.All;
        var input = new WebhookEndpointInput { Events = new[] { EventCatalog.Wildcard } };
        if (id is { } eid && await _webhooks.GetEndpointAsync(TenantId, eid, ct) is { } existing)
        {
            input.Id = existing.Id;
            input.Url = existing.Url;
            input.Events = existing.Events;
            input.IsActive = existing.IsActive;
            // Secret is write-only — never sent back to the form.
        }
        return View(input);
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(WebhookEndpointInput input, CancellationToken ct)
    {
        ViewData["Title"] = "Webhook endpoint";
        ViewData["Catalog"] = EventCatalog.All;
        try
        {
            await _webhooks.SaveEndpointAsync(TenantId, input, CurrentAudit(), ct);
            TempData["Success"] = "Webhook endpoint saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (WebhookConfigInvalidException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(nameof(Edit), input);
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _webhooks.DeleteEndpointAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Webhook endpoint removed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:guid}/test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct)
    {
        try
        {
            await _webhooks.SendTestAsync(TenantId, id, CurrentAudit(), ct);
            TempData["Success"] = "Test delivery queued.";
        }
        catch (WebhookConfigInvalidException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("deliveries/{deliveryId:guid}/retry")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(Guid deliveryId, CancellationToken ct)
    {
        await _webhooks.RetryDeliveryAsync(TenantId, deliveryId, CurrentAudit(), ct);
        TempData["Success"] = "Delivery re-queued.";
        return RedirectToAction(nameof(Index));
    }
}
