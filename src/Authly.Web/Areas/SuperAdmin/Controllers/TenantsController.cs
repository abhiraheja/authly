using Authly.Modules.Tenants;
using Authly.Web.Areas.SuperAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.SuperAdmin.Controllers;

public sealed class TenantsController : SuperAdminControllerBase
{
    private readonly ITenantService _tenants;

    public TenantsController(ITenantService tenants) => _tenants = tenants;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _tenants.ListAsync(ct));

    [HttpGet]
    public IActionResult Create() => View(new CreateTenantViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateTenantViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View(model);

        try
        {
            var tenant = await _tenants.CreateAsync(
                new CreateTenantRequest(model.Name, model.Slug), CurrentAudit(), ct);
            TempData["Success"] = $"Tenant '{tenant.Name}' created.";
            return RedirectToAction(nameof(Index));
        }
        catch (SlugAlreadyExistsException ex)
        {
            ModelState.AddModelError(nameof(model.Slug), ex.Message);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        await _tenants.SuspendAsync(id, CurrentAudit(), ct);
        TempData["Success"] = "Tenant suspended.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await _tenants.ReactivateAsync(id, CurrentAudit(), ct);
        TempData["Success"] = "Tenant reactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _tenants.DeleteAsync(id, CurrentAudit(), ct);
        TempData["Success"] = "Tenant deleted.";
        return RedirectToAction(nameof(Index));
    }
}
