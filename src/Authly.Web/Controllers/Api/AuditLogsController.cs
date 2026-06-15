using Authly.Core.Common;
using Authly.Core.Interfaces;
using Authly.Web.Infrastructure.Api;
using Authly.Web.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers.Api;

/// <summary>Management API: read-only, filterable audit log (§6).</summary>
[Route("api/v1/audit-logs")]
public sealed class AuditLogsController : ApiControllerBase
{
    private readonly IAuditLogRepository _audit;

    public AuditLogsController(IAuditLogRepository audit, ITenantContext tenant) : base(tenant) => _audit = audit;

    [HttpGet("")]
    [RequirePermission("audit.read")]
    public async Task<IActionResult> List(
        [FromQuery] int? page, [FromQuery] int? limit,
        [FromQuery] string? @event, [FromQuery] Guid? actorId, CancellationToken ct)
    {
        var p = Pagination.Of(page, limit);
        var result = await _audit.ListByTenantAsync(TenantId, p, @event, actorId, ct);
        return Ok(PagedResponse<AuditLogResponse>.From(result, p, AuditLogResponse.From));
    }
}
