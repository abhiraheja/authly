using System.Text.Json;
using System.Text.Json.Nodes;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Events;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.Webhooks;

/// <inheritdoc />
public sealed class WebhookService : IWebhookService
{
    private readonly IWebhookEndpointRepository _endpoints;
    private readonly IWebhookDeliveryRepository _deliveries;
    private readonly IWebhookSender _sender;
    private readonly IWebhookQueue _queue;
    private readonly IEncryptionService _encryption;
    private readonly ICredentialGenerator _credentials;
    private readonly IAuditLogger _audit;
    private readonly ILogger<WebhookService> _logger;

    public WebhookService(
        IWebhookEndpointRepository endpoints,
        IWebhookDeliveryRepository deliveries,
        IWebhookSender sender,
        IWebhookQueue queue,
        IEncryptionService encryption,
        ICredentialGenerator credentials,
        IAuditLogger audit,
        ILogger<WebhookService> logger)
    {
        _endpoints = endpoints;
        _deliveries = deliveries;
        _sender = sender;
        _queue = queue;
        _encryption = encryption;
        _credentials = credentials;
        _audit = audit;
        _logger = logger;
    }

    // --- Admin --------------------------------------------------------------

    public Task<IReadOnlyList<WebhookEndpoint>> ListEndpointsAsync(Guid tenantId, CancellationToken ct = default)
        => _endpoints.ListByTenantAsync(tenantId, ct);

    public Task<WebhookEndpoint?> GetEndpointAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _endpoints.GetByIdAsync(tenantId, id, ct);

    public async Task SaveEndpointAsync(Guid tenantId, WebhookEndpointInput input, AuditContext actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(input.Url) || !Uri.TryCreate(input.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new WebhookConfigInvalidException("Enter a valid absolute http(s) URL.");

        var events = (input.Events ?? Array.Empty<string>())
            .Where(e => !string.IsNullOrWhiteSpace(e)).Select(e => e.Trim()).Distinct().ToArray();
        if (events.Length == 0)
            throw new WebhookConfigInvalidException("Subscribe the endpoint to at least one event (or '*').");

        var existing = input.Id is { } id ? await _endpoints.GetByIdAsync(tenantId, id, ct) : null;

        if (existing is null)
        {
            // New endpoints always get a secret; generate one if the admin didn't supply it.
            var rawSecret = string.IsNullOrWhiteSpace(input.Secret) ? _credentials.GenerateClientSecret() : input.Secret!;
            await _endpoints.AddAsync(new WebhookEndpoint
            {
                TenantId = tenantId,
                Url = input.Url.Trim(),
                Events = events,
                Secret = _encryption.Encrypt(rawSecret),
                IsActive = input.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
        }
        else
        {
            existing.Url = input.Url.Trim();
            existing.Events = events;
            existing.IsActive = input.IsActive;
            // Secret is write-only: rotate only when a new value is supplied.
            if (!string.IsNullOrWhiteSpace(input.Secret))
                existing.Secret = _encryption.Encrypt(input.Secret!);
            await _endpoints.UpdateAsync(existing, ct);
        }

        await _audit.LogAsync("webhook.endpoint_saved", actor, tenantId, "webhook_endpoint", existing?.Id,
            metadata: new { input.Url, events, input.IsActive }, ct: ct);
    }

    public async Task DeleteEndpointAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var endpoint = await _endpoints.GetByIdAsync(tenantId, id, ct);
        if (endpoint is null) return;
        await _endpoints.DeleteAsync(endpoint, ct);
        await _audit.LogAsync("webhook.endpoint_deleted", actor, tenantId, "webhook_endpoint", id, ct: ct);
    }

    public Task<IReadOnlyList<WebhookDelivery>> ListRecentDeliveriesAsync(Guid tenantId, int take = 100, CancellationToken ct = default)
        => _deliveries.ListRecentByTenantAsync(tenantId, take, ct);

    public async Task RetryDeliveryAsync(Guid tenantId, Guid deliveryId, AuditContext actor, CancellationToken ct = default)
    {
        var delivery = await _deliveries.GetByIdAsync(deliveryId, ct);
        if (delivery is null || delivery.TenantId != tenantId) return;

        // Reset to pending and queue a fresh attempt now; attempts keeps climbing for the audit trail
        // but a manual retry resets the backoff so the next failure restarts the ladder.
        delivery.Status = WebhookDeliveryStatus.Pending;
        delivery.NextRetryAt = DateTimeOffset.UtcNow;
        await _deliveries.UpdateAsync(delivery, ct);

        _queue.Enqueue(tenantId, deliveryId);
        await _audit.LogAsync("webhook.delivery_retried", actor, tenantId, "webhook_delivery", deliveryId, ct: ct);
    }

    public async Task SendTestAsync(Guid tenantId, Guid endpointId, AuditContext actor, CancellationToken ct = default)
    {
        var endpoint = await _endpoints.GetByIdAsync(tenantId, endpointId, ct);
        if (endpoint is null) throw new WebhookConfigInvalidException("Endpoint not found.");

        const string testEvent = "webhook.test";
        var deliveryId = Guid.NewGuid();
        var body = BuildBody(deliveryId, new EventEnvelope(
            testEvent, tenantId, DateTimeOffset.UtcNow, actor.ActorId, actor.ActorType, "webhook_endpoint", endpointId,
            "success", new { message = "This is a test delivery from Authly." }));

        await _deliveries.AddAsync(new WebhookDelivery
        {
            Id = deliveryId,
            EndpointId = endpoint.Id,
            TenantId = tenantId,
            Event = testEvent,
            Payload = body,
            Status = WebhookDeliveryStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        _queue.Enqueue(tenantId, deliveryId);
        await _audit.LogAsync("webhook.test_sent", actor, tenantId, "webhook_endpoint", endpointId, ct: ct);
    }

    // --- Dispatch -----------------------------------------------------------

    public async Task DispatchAsync(Guid deliveryId, CancellationToken ct = default)
    {
        var delivery = await _deliveries.GetByIdAsync(deliveryId, ct);
        if (delivery is null || delivery.Status == WebhookDeliveryStatus.Success)
            return;

        var endpoint = await _endpoints.GetByIdAsync(delivery.TenantId, delivery.EndpointId, ct);
        if (endpoint is null || !endpoint.IsActive)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.NextRetryAt = null;
            delivery.LastError = endpoint is null ? "endpoint_deleted" : "endpoint_disabled";
            await _deliveries.UpdateAsync(delivery, ct);
            return;
        }

        var secret = DecryptOrNull(endpoint.Secret);
        if (secret is null)
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.NextRetryAt = null;
            delivery.LastError = "secret_unavailable";
            await _deliveries.UpdateAsync(delivery, ct);
            return;
        }

        var result = await _sender.SendAsync(
            new WebhookSendRequest(endpoint.Url, secret, delivery.Event, delivery.Id, delivery.Payload), ct);

        delivery.Attempts += 1;
        delivery.ResponseCode = result.StatusCode;

        if (result.Success)
        {
            delivery.Status = WebhookDeliveryStatus.Success;
            delivery.NextRetryAt = null;
            delivery.LastError = null;
            await _deliveries.UpdateAsync(delivery, ct);
            return;
        }

        delivery.LastError = result.Error;
        var delay = WebhookRetrySchedule.DelayAfter(delivery.Attempts);
        if (delay is { } d)
        {
            delivery.Status = WebhookDeliveryStatus.Pending;
            delivery.NextRetryAt = DateTimeOffset.UtcNow.Add(d);
            await _deliveries.UpdateAsync(delivery, ct);
            _queue.Schedule(delivery.TenantId, delivery.Id, d);
        }
        else
        {
            delivery.Status = WebhookDeliveryStatus.Failed;
            delivery.NextRetryAt = null;
            await _deliveries.UpdateAsync(delivery, ct);
            _logger.LogWarning("Webhook delivery {DeliveryId} permanently failed after {Attempts} attempts.",
                delivery.Id, delivery.Attempts);
        }
    }

    // --- helpers ------------------------------------------------------------

    internal static string BuildBody(Guid deliveryId, EventEnvelope env)
    {
        var obj = new JsonObject
        {
            ["id"] = deliveryId.ToString(),
            ["event"] = env.Event,
            ["tenant_id"] = env.TenantId.ToString(),
            ["occurred_at"] = env.OccurredAt.ToString("O"),
            ["result"] = env.Result
        };
        if (env.ActorId is { } actorId) obj["actor_id"] = actorId.ToString();
        if (env.ActorType is { } actorType) obj["actor_type"] = actorType;
        if (env.ResourceType is { } rt) obj["resource_type"] = rt;
        if (env.ResourceId is { } rid) obj["resource_id"] = rid.ToString();
        obj["data"] = env.Data is null ? new JsonObject() : JsonNode.Parse(JsonSerializer.Serialize(env.Data));
        return obj.ToJsonString();
    }

    private string? DecryptOrNull(string cipher)
    {
        try { return _encryption.Decrypt(cipher); }
        catch (Exception) { return null; } // rotated/corrupt key — fail this delivery rather than crash
    }
}
