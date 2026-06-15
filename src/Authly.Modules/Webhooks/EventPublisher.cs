using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Events;
using Authly.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.Webhooks;

/// <summary>
/// Fans a domain event out to every subscribed, active endpoint (§4.12): records one pending
/// delivery per endpoint with the signed body baked in, then enqueues each for dispatch. Swallows
/// all errors — publishing is best-effort and must never break the operation that raised the event.
/// </summary>
public sealed class EventPublisher : IEventPublisher
{
    private readonly IWebhookEndpointRepository _endpoints;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IWebhookQueue _queue;
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(
        IWebhookEndpointRepository endpoints,
        IWebhookDeliveryRepository deliveries,
        IWebhookQueue queue,
        ILogger<EventPublisher> logger)
    {
        _endpoints = endpoints;
        _deliveries = deliveries;
        _queue = queue;
        _logger = logger;
    }

    public async Task PublishAsync(EventEnvelope envelope, CancellationToken ct = default)
    {
        try
        {
            var endpoints = await _endpoints.ListMatchingAsync(envelope.TenantId, envelope.Event, ct);
            if (endpoints.Count == 0)
                return;

            foreach (var endpoint in endpoints)
            {
                var deliveryId = Guid.NewGuid();
                var body = WebhookService.BuildBody(deliveryId, envelope);

                await _deliveries.AddAsync(new WebhookDelivery
                {
                    Id = deliveryId,
                    EndpointId = endpoint.Id,
                    TenantId = envelope.TenantId,
                    Event = envelope.Event,
                    Payload = body,
                    Status = WebhookDeliveryStatus.Pending,
                    CreatedAt = DateTimeOffset.UtcNow
                }, ct);

                _queue.Enqueue(envelope.TenantId, deliveryId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to publish event {Event} for tenant {TenantId}.",
                envelope.Event, envelope.TenantId);
        }
    }
}
