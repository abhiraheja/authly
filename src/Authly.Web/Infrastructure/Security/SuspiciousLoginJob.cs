using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Security;

namespace Authly.Web.Infrastructure.Security;

/// <summary>
/// Hangfire job that evaluates a completed login for new-device/new-location anomalies and alerts
/// the user. Runs outside the HTTP request, so it binds the tenant context first for RLS.
/// </summary>
public sealed class SuspiciousLoginJob
{
    private readonly ISuspiciousLoginService _suspicious;
    private readonly ITenantContext _tenant;

    public SuspiciousLoginJob(ISuspiciousLoginService suspicious, ITenantContext tenant)
    {
        _suspicious = suspicious;
        _tenant = tenant;
    }

    public Task EvaluateAsync(Guid tenantId, Guid userId, string? ip, string? userAgent)
    {
        _tenant.SetTenant(tenantId);
        return _suspicious.EvaluateAsync(tenantId, userId, new RequestInfo(ip, userAgent));
    }
}
