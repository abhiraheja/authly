using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Tenants;
using Authly.Web.Areas.SuperAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.SuperAdmin.Controllers;

public sealed class TenantsController : SuperAdminControllerBase
{
    private readonly ITenantService _tenants;
    private readonly IOrganizationRepository _organizations;

    public TenantsController(ITenantService tenants, IOrganizationRepository organizations)
    {
        _tenants = tenants;
        _organizations = organizations;
    }

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
            // A project always lives inside an organization. The (legacy, deprecated) super-admin
            // create path has no founding account, so provision a platform-owned org (null owner).
            var org = await CreatePlatformOrganizationAsync(model.Name, ct);
            var tenant = await _tenants.CreateAsync(
                new CreateTenantRequest(model.Name, model.Slug, org.Id), CurrentAudit(), ct);
            TempData["Success"] = $"Tenant '{tenant.Name}' created.";
            return RedirectToAction(nameof(Index));
        }
        catch (SlugAlreadyExistsException ex)
        {
            ModelState.AddModelError(nameof(model.Slug), ex.Message);
            return View(model);
        }
    }

    // Allocates a platform-owned organization (no founding account) with a globally-unique slug.
    private async Task<Organization> CreatePlatformOrganizationAsync(string name, CancellationToken ct)
    {
        var baseSlug = TenantService.Slugify(name);
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 0; ; attempt++)
        {
            var slug = attempt == 0 ? baseSlug : $"{baseSlug}-{attempt + 1}";
            if (await _organizations.SlugExistsAsync(slug, ct)) continue;
            var org = new Organization { Name = name.Trim(), Slug = slug, OwnerAccountId = null, CreatedAt = now, UpdatedAt = now };
            await _organizations.AddAsync(org, ct);
            return org;
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
