using Authly.Core.Entities;
using Authly.Core.Enums;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for tenant webhook endpoints (§4.12). Tenant-scoped.</summary>
public interface IWebhookEndpointRepository
{
    Task<IReadOnlyList<WebhookEndpoint>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Active endpoints whose subscription matches <paramref name="eventName"/> (or the wildcard).</summary>
    Task<IReadOnlyList<WebhookEndpoint>> ListMatchingAsync(Guid tenantId, string eventName, CancellationToken ct = default);

    Task<WebhookEndpoint?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
    Task DeleteAsync(WebhookEndpoint endpoint, CancellationToken ct = default);
}

/// <summary>Persistence for webhook deliveries + their retry state (§4.12). Tenant-scoped.</summary>
public interface IWebhookDeliveryRepository
{
    Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default);
    Task UpdateAsync(WebhookDelivery delivery, CancellationToken ct = default);

    /// <summary>Loads a delivery by id without a tenant filter (dispatch runs outside a request).</summary>
    Task<WebhookDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<WebhookDelivery>> ListRecentByTenantAsync(Guid tenantId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<WebhookDelivery>> ListByEndpointAsync(Guid tenantId, Guid endpointId, int take, CancellationToken ct = default);
}

/// <summary>Persistence for tenant pipeline hooks (§4.12). Tenant-scoped.</summary>
public interface IPipelineHookRepository
{
    Task<IReadOnlyList<PipelineHook>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Active hooks bound to a stage, in a stable order.</summary>
    Task<IReadOnlyList<PipelineHook>> ListActiveByStageAsync(Guid tenantId, PipelineStage stage, CancellationToken ct = default);

    Task<PipelineHook?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(PipelineHook hook, CancellationToken ct = default);
    Task UpdateAsync(PipelineHook hook, CancellationToken ct = default);
    Task DeleteAsync(PipelineHook hook, CancellationToken ct = default);
}

/// <summary>Persistence for tenant custom-claim config (§4.13). Tenant-scoped.</summary>
public interface IClaimConfigRepository
{
    Task<IReadOnlyList<ClaimConfig>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Configs that apply to a token issued for an application (tenant-wide rows + that app's rows).</summary>
    Task<IReadOnlyList<ClaimConfig>> ListForIssuanceAsync(Guid tenantId, Guid? applicationId, CancellationToken ct = default);

    Task<ClaimConfig?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(ClaimConfig config, CancellationToken ct = default);
    Task DeleteAsync(ClaimConfig config, CancellationToken ct = default);
}
