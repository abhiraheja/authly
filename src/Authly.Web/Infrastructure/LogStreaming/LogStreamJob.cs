using System.Globalization;
using System.Net.Http.Json;
using Authly.Core.Interfaces;
using Authly.Core.Logging;
using Authly.Modules.Observability;

namespace Authly.Web.Infrastructure.LogStreaming;

/// <summary>
/// Streams audit-log entries to an external sink (SIEM / webhook) for the operator. Runs as a
/// Hangfire recurring job: reads new entries since a persisted cursor and POSTs them in batches,
/// advancing the cursor only on a successful delivery (at-least-once). Best-effort — failures are
/// logged and retried next cycle; streaming never affects authentication. The target is sourced from
/// the stored observability config (Phase 7), falling back to the LOG_STREAM_* env vars.
/// </summary>
public sealed class LogStreamJob
{
    public const string CursorKey = "log_stream.cursor";
    private const int BatchSize = 200;

    private readonly IConfiguration _config;
    private readonly IObservabilityConfigService _observability;
    private readonly IAuditLogStreamSource _source;
    private readonly IPlatformStateStore _state;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<LogStreamJob> _logger;

    public LogStreamJob(IConfiguration config, IObservabilityConfigService observability, IAuditLogStreamSource source,
        IPlatformStateStore state, IHttpClientFactory httpFactory, ILogger<LogStreamJob> logger)
    {
        _config = config;
        _observability = observability;
        _source = source;
        _state = state;
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task FlushAsync(CancellationToken ct = default)
    {
        // Stored config wins; env vars are the fallback for headless/bootstrap deployments.
        var settings = await _observability.GetSettingsAsync(ct);
        var endpoint = settings.LogStreamEndpoint ?? _config["LOG_STREAM_ENDPOINT"];
        if (string.IsNullOrWhiteSpace(endpoint)) return;
        var apiKey = settings.LogStreamKey ?? _config["LOG_STREAM_KEY"];

        try
        {
            var cursorRaw = await _state.GetAsync(CursorKey, ct);
            var cursor = DateTimeOffset.TryParse(cursorRaw, CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind, out var c) ? c : DateTimeOffset.MinValue;

            var batch = await _source.ReadAfterAsync(cursor, BatchSize, ct);
            if (batch.Count == 0) return;

            var payload = batch.Select(a => new
            {
                id = a.Id,
                tenant_id = a.TenantId,
                actor_id = a.ActorId,
                actor_type = a.ActorType,
                @event = a.Event,
                resource_type = a.ResourceType,
                resource_id = a.ResourceId,
                ip = a.IpAddress,
                result = a.Result,
                metadata = a.Metadata,
                created_at = a.CreatedAt
            }).ToList();

            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(15);
            using var req = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = JsonContent.Create(payload) };
            if (!string.IsNullOrWhiteSpace(apiKey)) req.Headers.Add("X-Log-Stream-Key", apiKey);

            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Log stream sink rejected batch: {Status}; will retry.", (int)resp.StatusCode);
                return; // don't advance cursor — re-send next cycle.
            }

            // Advance the cursor to the newest delivered entry.
            var newest = batch[^1].CreatedAt;
            await _state.SetAsync(CursorKey, newest.ToString("O", CultureInfo.InvariantCulture), ct);
            _logger.LogInformation("Streamed {Count} audit entries to the log sink.", batch.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Log streaming failed; will retry next cycle.");
        }
    }
}
