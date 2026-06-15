using Hangfire.Dashboard;

namespace Saarvix.Identity.Web.Infrastructure;

/// <summary>
/// Gate for the Hangfire dashboard. In Development it is open; in other environments
/// it denies all until super-admin authentication is wired up (Phase 1 / Step 2).
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _allowAll;

    public HangfireDashboardAuthorizationFilter(bool allowAll) => _allowAll = allowAll;

    public bool Authorize(DashboardContext context) => _allowAll;
}
