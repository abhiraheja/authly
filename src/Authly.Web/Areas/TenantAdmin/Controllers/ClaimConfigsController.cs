using Authly.Core.Interfaces;
using Authly.Modules.Applications;
using Authly.Modules.Claims;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Tenant-admin configuration of custom token claims (§4.13 / Phase 9): static + metadata-mapped
/// claims for id/access tokens. Webhook-sourced claims are configured as pre-token pipeline hooks.
/// </summary>
[Route("tenantadmin/claims")]
public sealed class ClaimConfigsController : TenantAdminControllerBase
{
    private readonly IClaimConfigService _claims;
    private readonly IApplicationService _applications;

    public ClaimConfigsController(IClaimConfigService claims, IApplicationService applications, ITenantContext tenant)
        : base(tenant)
    {
        _claims = claims;
        _applications = applications;
    }

    [RequireOperatorPermission("project.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Custom claims";
        ViewData["Applications"] = await _applications.ListAsync(TenantId, ct);
        ViewData["Configs"] = await _claims.ListAsync(TenantId, ct);
        return View(new ClaimConfigInput());
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(ClaimConfigInput input, CancellationToken ct)
    {
        try
        {
            await _claims.AddAsync(TenantId, input, CurrentAudit(), ct);
            TempData["Success"] = $"Claim '{input.ClaimName}' added.";
        }
        catch (ClaimConfigInvalidException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _claims.DeleteAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Claim removed.";
        return RedirectToAction(nameof(Index));
    }
}
