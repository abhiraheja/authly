using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Webhooks;

/// <summary>
/// Tenant webhook administration plus the dispatch entry point used by the background job (§4.12).
/// </summary>
public interface IWebhookService
{
    // --- Admin ---
    Task<IReadOnlyList<WebhookEndpoint>> ListEndpointsAsync(Guid tenantId, CancellationToken ct = default);
    Task<WebhookEndpoint?> GetEndpointAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task SaveEndpointAsync(Guid tenantId, WebhookEndpointInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteEndpointAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<WebhookDelivery>> ListRecentDeliveriesAsync(Guid tenantId, int take = 100, CancellationToken ct = default);

    /// <summary>Re-queue a delivery for an immediate fresh attempt (manual retry from the dashboard).</summary>
    Task RetryDeliveryAsync(Guid tenantId, Guid deliveryId, AuditContext actor, CancellationToken ct = default);

    /// <summary>Fire a synthetic <c>webhook.test</c> event at one endpoint to verify the integration.</summary>
    Task SendTestAsync(Guid tenantId, Guid endpointId, AuditContext actor, CancellationToken ct = default);

    // --- Dispatch (called by the Hangfire job) ---

    /// <summary>
    /// Attempt one delivery. On a 2xx the delivery is marked success; otherwise attempts is advanced
    /// and the next retry is scheduled on the backoff ladder, or the delivery is marked failed once
    /// the ladder is exhausted.
    /// </summary>
    Task DispatchAsync(Guid deliveryId, CancellationToken ct = default);
}
