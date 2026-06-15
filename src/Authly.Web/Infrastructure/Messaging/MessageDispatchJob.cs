using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Messaging;

namespace Authly.Web.Infrastructure.Messaging;

/// <summary>
/// Hangfire-invoked job that delivers a queued <see cref="MessageSendRequest"/> via the messaging
/// service. Because it runs outside an HTTP request (no tenant-resolution middleware), it binds
/// the tenant from the request so the RLS backstop applies to the messaging-table reads/writes.
/// </summary>
public sealed class MessageDispatchJob
{
    private readonly IMessagingService _messaging;
    private readonly ITenantContext _tenant;

    public MessageDispatchJob(IMessagingService messaging, ITenantContext tenant)
    {
        _messaging = messaging;
        _tenant = tenant;
    }

    public Task DispatchAsync(MessageSendRequest request)
    {
        if (!_tenant.HasTenant) _tenant.SetTenant(request.TenantId);
        return _messaging.DeliverAsync(request);
    }
}
