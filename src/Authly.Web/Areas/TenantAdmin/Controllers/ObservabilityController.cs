using Authly.Core.Interfaces;
using Authly.Modules.Observability;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Instance-global observability (OpenTelemetry) configuration — BYOK exporter settings with
/// write-only encrypted secrets, mirroring the messaging-provider UX. Gated on
/// <c>observability.read</c>/<c>observability.manage</c>. Changes apply on the next app restart.
/// </summary>
[Route("tenantadmin/observability")]
public sealed class ObservabilityController : TenantAdminControllerBase
{
    private readonly IObservabilityConfigService _observability;

    public ObservabilityController(IObservabilityConfigService observability, ITenantContext tenant) : base(tenant)
        => _observability = observability;

    [RequireOperatorPermission("observability.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Observability";
        var c = await _observability.GetForEditAsync(ct);
        var signals = c.Signals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        return View(new ObservabilityViewModel
        {
            Enabled = c.Enabled,
            Exporter = c.Exporter,
            OtlpEndpoint = c.OtlpEndpoint,
            HasOtlpHeaders = c.HasOtlpHeaders,
            HasAzureConnectionString = c.HasAzureConnectionString,
            ExportTraces = signals.Contains("traces"),
            ExportMetrics = signals.Contains("metrics"),
            ExportLogs = signals.Contains("logs"),
            SamplingRatio = c.SamplingRatio,
            LogStreamEndpoint = c.LogStreamEndpoint,
            HasLogStreamKey = c.HasLogStreamKey,
            UpdatedAt = c.UpdatedAt
        });
    }

    [RequireOperatorPermission("observability.manage")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ObservabilityViewModel model, CancellationToken ct)
    {
        ViewData["Title"] = "Observability";
        if (!ModelState.IsValid) return View(model);

        var signals = new List<string>();
        if (model.ExportTraces) signals.Add("traces");
        if (model.ExportMetrics) signals.Add("metrics");
        if (model.ExportLogs) signals.Add("logs");

        await _observability.SaveAsync(new ObservabilityConfigInput
        {
            Enabled = model.Enabled,
            Exporter = model.Exporter,
            OtlpEndpoint = model.OtlpEndpoint,
            OtlpHeaders = model.OtlpHeaders,
            AzureConnectionString = model.AzureConnectionString,
            Signals = string.Join(",", signals),
            SamplingRatio = model.SamplingRatio,
            LogStreamEndpoint = model.LogStreamEndpoint,
            LogStreamKey = model.LogStreamKey
        }, CurrentAudit(), ct);

        TempData["Success"] = "Observability settings saved. Restart the app for exporter changes to take effect.";
        return RedirectToAction(nameof(Index));
    }
}
