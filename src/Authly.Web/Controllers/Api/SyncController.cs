using Authly.Modules.Compliance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers.Api;

/// <summary>
/// Cloud control-plane ingest for self-hosted telemetry (§9). A self-hosted instance POSTs its
/// aggregate metrics here every ~6h, authenticated by its issued sync key in the
/// <c>X-Sync-Key</c> header. Accepts numbers only — never PII. Not tenant-scoped.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/sync")]
[Produces("application/json")]
public sealed class SyncController : ControllerBase
{
    public const string SyncKeyHeader = "X-Sync-Key";

    private readonly ISelfHostSyncService _sync;

    public SyncController(ISelfHostSyncService sync) => _sync = sync;

    /// <summary>Aggregate-only telemetry payload. Any field beyond these is ignored by the binder.</summary>
    public sealed record SyncRequest(
        string? Version, int TenantCount, int UserCount, int AppCount, int ActiveSessionCount, string? Status);

    [HttpPost("")]
    public async Task<IActionResult> Ingest([FromBody] SyncRequest body, CancellationToken ct)
    {
        if (!Request.Headers.TryGetValue(SyncKeyHeader, out var key) || string.IsNullOrWhiteSpace(key))
            return Unauthorized(new { error = "missing_sync_key" });

        var payload = new SyncPayload(
            body.Version ?? "",
            body.TenantCount, body.UserCount, body.AppCount, body.ActiveSessionCount,
            body.Status ?? "unknown");

        var accepted = await _sync.IngestAsync(key.ToString(), payload, ct);
        return accepted ? Ok(new { status = "ok" }) : Unauthorized(new { error = "invalid_sync_key" });
    }
}
