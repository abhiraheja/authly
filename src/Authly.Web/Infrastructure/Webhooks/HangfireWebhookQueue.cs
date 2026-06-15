using Authly.Core.Events;
using Hangfire;

namespace Authly.Web.Infrastructure.Webhooks;

/// <summary>
/// <see cref="IWebhookQueue"/> backed by Hangfire. Immediate enqueue for first attempts/manual
/// retries; scheduled enqueue for the exponential-backoff retry ladder. The tenant id rides along
/// so <see cref="WebhookDispatchJob"/> can set the RLS scope before loading the delivery.
/// </summary>
public sealed class HangfireWebhookQueue : IWebhookQueue
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireWebhookQueue(IBackgroundJobClient jobs) => _jobs = jobs;

    public void Enqueue(Guid tenantId, Guid deliveryId)
        => _jobs.Enqueue<WebhookDispatchJob>(job => job.DispatchAsync(tenantId, deliveryId));

    public void Schedule(Guid tenantId, Guid deliveryId, TimeSpan delay)
        => _jobs.Schedule<WebhookDispatchJob>(job => job.DispatchAsync(tenantId, deliveryId), delay);
}
