using Authly.Core.Compliance;
using Authly.Core.Deployment;
using Authly.Core.Interfaces;
using Authly.Core.Monitoring;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.SuperAdmin.Controllers;

/// <summary>
/// Platform monitoring &amp; ops: dependency health, aggregate metrics, login analytics, self-hosted
/// instance telemetry, version/deprecation, and backup status. All counts are aggregate-only.
/// </summary>
public sealed class MonitoringController : SuperAdminControllerBase
{
    private readonly IPlatformHealthProbe _health;
    private readonly IInstanceMetricsCollector _metrics;
    private readonly ILoginAnalyticsStore _analytics;
    private readonly ISelfHostedInstanceRepository _instances;
    private readonly IDeploymentContext _deployment;
    private readonly IConfiguration _config;

    public MonitoringController(
        IPlatformHealthProbe health, IInstanceMetricsCollector metrics, ILoginAnalyticsStore analytics,
        ISelfHostedInstanceRepository instances, IDeploymentContext deployment, IConfiguration config)
    {
        _health = health;
        _metrics = metrics;
        _analytics = analytics;
        _instances = instances;
        _deployment = deployment;
        _config = config;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewBag.Health = await _health.CheckAsync(ct);
        ViewBag.Metrics = await _metrics.CollectAsync(ct);
        ViewBag.Analytics = await _analytics.DailyOutcomesAsync(14, ct);
        ViewBag.Instances = await _instances.ListAsync(ct);

        ViewBag.Version = _deployment.Version;
        ViewBag.DeploymentMode = _deployment.Mode.ToString();
        ViewBag.DeprecationNotice = _config["DEPRECATION_NOTICE"];
        ViewBag.BackupStatus = _config["BACKUP_STATUS"];
        ViewBag.BackupLastAt = _config["BACKUP_LAST_AT"];
        return View();
    }
}
