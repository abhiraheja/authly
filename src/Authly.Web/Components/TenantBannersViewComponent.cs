using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Announcements;
using Authly.Modules.Tenants;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Components;

/// <summary>
/// Renders the tenant-admin notice strip: an onboarding nudge while setup is incomplete plus any
/// active platform announcements. Read-only and best-effort — a data hiccup never breaks the page.
/// </summary>
public sealed class TenantBannersViewComponent : ViewComponent
{
    private readonly ITenantContext _tenant;
    private readonly ITenantService _tenants;
    private readonly IAnnouncementService _announcements;

    public TenantBannersViewComponent(ITenantContext tenant, ITenantService tenants, IAnnouncementService announcements)
    {
        _tenant = tenant;
        _tenants = tenants;
        _announcements = announcements;
    }

    public async Task<IViewComponentResult> InvokeAsync(bool showOnboarding = true)
    {
        var model = new TenantBannersModel();
        try
        {
            if (showOnboarding && _tenant.HasTenant)
                model.ShowOnboarding = !await _tenants.IsOnboardedAsync(_tenant.TenantId!.Value);

            model.Announcements = await _announcements.ListVisibleAsync();
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
    public IReadOnlyList<Announcement> Announcements { get; set; } = Array.Empty<Announcement>();
}
