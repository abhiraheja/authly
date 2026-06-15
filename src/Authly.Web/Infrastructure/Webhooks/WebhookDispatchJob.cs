using Authly.Core.Interfaces;
using Authly.Modules.Webhooks;

namespace Authly.Web.Infrastructure.Webhooks;

/// <summary>
/// Hangfire-invoked job that delivers one queued webhook. It runs outside an HTTP request (no
/// tenant-resolution middleware), so it binds the tenant from the job arguments to set the RLS
/// backstop before the webhook service loads + sends the delivery.
/// </summary>
public sealed class WebhookDispatchJob
{
    private readonly IWebhookService _webhooks;
    private readonly ITenantContext _tenant;

    public WebhookDispatchJob(IWebhookService webhooks, ITenantContext tenant)
    {
        _webhooks = webhooks;
        _tenant = tenant;
    }

    public Task DispatchAsync(Guid tenantId, Guid deliveryId)
    {
        if (!_tenant.HasTenant) _tenant.SetTenant(tenantId);
        return _webhooks.DispatchAsync(deliveryId);
    }
}
