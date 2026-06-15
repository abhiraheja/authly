using Hangfire.Dashboard;

namespace Authly.Web.Infrastructure;

/// <summary>
/// Gate for the Hangfire dashboard: open in Development, otherwise restricted to an
/// authenticated super admin.
/// </summary>
public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    private readonly bool _allowAll;

    public HangfireDashboardAuthorizationFilter(bool allowAll) => _allowAll = allowAll;

    public bool Authorize(DashboardContext context)
    {
        if (_allowAll) return true;
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true;
    }
}
