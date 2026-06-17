using Authly.Core.Interfaces;
using Authly.Modules.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Components;

/// <summary>
/// Renders the tenant-admin notice strip: an onboarding nudge while setup is incomplete.
/// Read-only and best-effort — a data hiccup never breaks the page.
/// </summary>
public sealed class TenantBannersViewComponent : ViewComponent
{
    private readonly ITenantContext _tenant;
    private readonly ITenantService _tenants;

    public TenantBannersViewComponent(ITenantContext tenant, ITenantService tenants)
    {
        _tenant = tenant;
        _tenants = tenants;
    }

    public async Task<IViewComponentResult> InvokeAsync(bool showOnboarding = true)
    {
        var model = new TenantBannersModel();
        try
        {
            if (showOnboarding && _tenant.HasTenant)
                model.ShowOnboarding = !await _tenants.IsOnboardedAsync(_tenant.TenantId!.Value);
        }
        catch
        {
            // Banners are decorative — never surface infrastructure errors here.
        }
        return View(model);
    }
}

public sealed class TenantBannersModel
{
    public bool ShowOnboarding { get; set; }
}
