using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Web.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers.Api;

/// <summary>Management API: the tenant's permission catalogue (§6).</summary>
[Route("api/v1/permissions")]
public sealed class PermissionsController : ApiControllerBase
{
    private readonly IRbacService _rbac;

    public PermissionsController(IRbacService rbac, ITenantContext tenant) : base(tenant) => _rbac = rbac;

    [HttpGet("")]
    [RequirePermission("role.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        await _rbac.EnsureSystemRolesAsync(TenantId, ct);
        var permissions = await _rbac.ListPermissionsAsync(TenantId, ct);
        return Ok(permissions.Select(PermissionResponse.From).ToList());
    }
}
