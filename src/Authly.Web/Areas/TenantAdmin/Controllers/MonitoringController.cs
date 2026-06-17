using System.Reflection;
using Authly.Core.Compliance;
using Authly.Core.Interfaces;
using Authly.Core.Monitoring;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Instance monitoring on the console (self-host owner == operator): dependency health, aggregate
/// instance counts, and login analytics. Read-only; gated on <c>observability.read</c>. Replaces the
/// deleted SuperAdmin monitoring surface (doc 04 — monitoring moves to the account surface).
/// </summary>
[Route("tenantadmin/monitoring")]
public sealed class MonitoringController : TenantAdminControllerBase
{
    private readonly IPlatformHealthProbe _health;
    private readonly IInstanceMetricsCollector _metrics;
    private readonly ILoginAnalyticsStore _analytics;

    public MonitoringController(
        IPlatformHealthProbe health,
        IInstanceMetricsCollector metrics,
        ILoginAnalyticsStore analytics,
        ITenantContext tenant) : base(tenant)
    {
        _health = health;
        _metrics = metrics;
        _analytics = analytics;
    }

    [RequireOperatorPermission("observability.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Monitoring";
        return View(new MonitoringViewModel
        {
            Health = await _health.CheckAsync(ct),
            Metrics = await _metrics.CollectAsync(ct),
            Analytics = await _analytics.DailyOutcomesAsync(14, ct),
            Version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "—"
        });
    }
}
