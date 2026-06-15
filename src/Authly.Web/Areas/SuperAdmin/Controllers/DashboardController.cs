using Authly.Core.Enums;
using Authly.Modules.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.SuperAdmin.Controllers;

public sealed class DashboardController : SuperAdminControllerBase
{
    private readonly ITenantService _tenants;

    public DashboardController(ITenantService tenants) => _tenants = tenants;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var tenants = await _tenants.ListAsync(ct);
        ViewBag.Total = tenants.Count;
        ViewBag.Active = tenants.Count(t => t.Status == TenantStatus.Active);
        ViewBag.Suspended = tenants.Count(t => t.Status == TenantStatus.Suspended);
        ViewBag.Recent = tenants.Take(5).ToList();
        return View();
    }
}
