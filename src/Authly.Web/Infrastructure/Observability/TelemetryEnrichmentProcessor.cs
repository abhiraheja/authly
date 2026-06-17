using System.Diagnostics;
using System.Security.Claims;
using OpenTelemetry;

namespace Authly.Web.Infrastructure.Observability;

/// <summary>
/// Stamps each span with the active tenant/project, organization, and account (from the request's
/// signed-in principal) so exported traces can be filtered per project. Reads the same claims the
/// console/portal cookies carry; no-ops for unauthenticated or non-HTTP activity.
/// </summary>
public sealed class TelemetryEnrichmentProcessor : BaseProcessor<Activity>
{
    private readonly IHttpContextAccessor _http;

    public TelemetryEnrichmentProcessor(IHttpContextAccessor http) => _http = http;

    public override void OnEnd(Activity activity)
    {
        var user = _http.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true) return;

        // Console (operator) cookie carries account/org/project; end-user cookie carries tenant only.
        var projectId = user.FindFirstValue(TenantAdminClaims.TenantId) ?? user.FindFirstValue(UserClaims.TenantId);
        if (projectId is not null)
        {
            activity.SetTag("tenant.id", projectId);
            activity.SetTag("project.id", projectId);
        }
        if (user.FindFirstValue(TenantAdminClaims.OrgId) is { } orgId) activity.SetTag("org.id", orgId);
        if (user.FindFirstValue(TenantAdminClaims.AccountId) is { } accountId) activity.SetTag("account.id", accountId);
    }
}
