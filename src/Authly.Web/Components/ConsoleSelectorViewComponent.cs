using System.Security.Claims;
using Authly.Core.Authorization;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Operators;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Components;

/// <summary>
/// Topbar two-level workspace selector (doc 06 §6): active <em>org › project</em> breadcrumb that
/// opens a switcher — other orgs (only when the account belongs to more than one), the active org's
/// projects (active checked), and "+ New project" (shown only with <c>project.create</c>). Read-only
/// and best-effort; a data hiccup collapses it to a static label rather than breaking the page.
/// </summary>
public sealed class ConsoleSelectorViewComponent : ViewComponent
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IOrganizationRepository _organizations;
    private readonly ITenantRepository _tenants;
    private readonly IConsoleAccessService _access;

    public ConsoleSelectorViewComponent(
        IOrganizationMembershipRepository memberships,
        IOrganizationRepository organizations,
        ITenantRepository tenants,
        IConsoleAccessService access)
    {
        _memberships = memberships;
        _organizations = organizations;
        _tenants = tenants;
        _access = access;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var model = new ConsoleSelectorModel();
        try
        {
            var user = (ClaimsPrincipal)User;
            if (!Guid.TryParse(user.FindFirstValue(TenantAdminClaims.AccountId) ?? user.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId)
                || !Guid.TryParse(user.FindFirstValue(TenantAdminClaims.OrgId), out var orgId)
                || !Guid.TryParse(user.FindFirstValue(TenantAdminClaims.TenantId), out var projectId))
                return View(model);

            model.ActiveOrgId = orgId;
            model.ActiveProjectId = projectId;

            // Orgs the account is an Active member of.
            var memberships = await _memberships.ListByAccountAsync(accountId, ct: default);
            foreach (var m in memberships.Where(m => m.Status == MembershipStatus.Active))
            {
                if (await _organizations.GetByIdAsync(m.OrganizationId) is { } org)
                    model.Orgs.Add(new SelectorItem(org.Id, org.Name, org.Id == orgId));
            }

            // Projects in the active org.
            foreach (var p in await _tenants.ListByOrganizationAsync(orgId))
                model.Projects.Add(new SelectorItem(p.Id, p.Name, p.Id == projectId));

            model.ActiveOrgName = model.Orgs.FirstOrDefault(o => o.IsActive)?.Name ?? "Organization";
            model.ActiveProjectName = model.Projects.FirstOrDefault(p => p.IsActive)?.Name ?? "Project";

            var access = await _access.ResolveAsync(accountId, orgId, projectId);
            model.CanCreateProject = access is not null && PermissionEvaluator.Satisfies(access.Permissions, "project.create");
        }
        catch
        {
            // Decorative chrome — never surface infrastructure errors here.
        }
        return View(model);
    }
}

public sealed record SelectorItem(Guid Id, string Name, bool IsActive);

public sealed class ConsoleSelectorModel
{
    public Guid ActiveOrgId { get; set; }
    public Guid ActiveProjectId { get; set; }
    public string ActiveOrgName { get; set; } = "Organization";
    public string ActiveProjectName { get; set; } = "Project";
    public List<SelectorItem> Orgs { get; } = new();
    public List<SelectorItem> Projects { get; } = new();
    public bool CanCreateProject { get; set; }
}
