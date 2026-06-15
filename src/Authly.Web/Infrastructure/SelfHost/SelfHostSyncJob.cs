using System.Net.Http.Json;
using Authly.Core.Compliance;
using Authly.Core.Deployment;
using Authly.Web.Controllers.Api;

namespace Authly.Web.Infrastructure.SelfHost;

/// <summary>
/// Self-hosted telemetry push (§9). Runs on the instance via a Hangfire recurring job (~6h):
/// collects AGGREGATE counts only and POSTs them to the cloud ingest with the instance's sync key.
/// Best-effort by contract — any failure is swallowed and logged; it must NEVER block auth, and
/// the instance keeps running normally whether or not cloud is reachable (30+ day offline grace).
/// </summary>
public sealed class SelfHostSyncJob
{
    private readonly IDeploymentContext _deployment;
    private readonly IInstanceMetricsCollector _metrics;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<SelfHostSyncJob> _logger;

    public SelfHostSyncJob(IDeploymentContext deployment, IInstanceMetricsCollector metrics,
        IHttpClientFactory httpFactory, ILogger<SelfHostSyncJob> logger)
    {
        _deployment = deployment;
        _metrics = metrics;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task PushAsync(CancellationToken ct = default)
    {
        if (!_deployment.SyncEnabled)
            return; // cloud mode, or sync not configured — nothing to do.

        try
        {
            var m = await _metrics.CollectAsync(ct);
            var body = new SyncController.SyncRequest(
                _deployment.Version, m.TenantCount, m.UserCount, m.AppCount, m.ActiveSessionCount, "ok");

            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);

            using var req = new HttpRequestMessage(HttpMethod.Post, _deployment.SyncEndpoint);
            req.Headers.Add(SyncController.SyncKeyHeader, _deployment.SyncKey);
            req.Content = JsonContent.Create(body);

            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
                _logger.LogInformation("Self-host telemetry sync succeeded.");
            else
                _logger.LogWarning("Self-host telemetry sync rejected: {Status}.", (int)resp.StatusCode);
        }
        catch (Exception ex)
        {
            // Best-effort: never propagate. Will retry on the next scheduled run.
            _logger.LogWarning(ex, "Self-host telemetry sync failed; will retry next cycle.");
        }
    }
}
