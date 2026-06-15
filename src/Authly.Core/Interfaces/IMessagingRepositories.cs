using Authly.Core.Entities;
using Authly.Core.Enums;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for tenant messaging-provider config. Tenant-scoped.</summary>
public interface IMessagingProviderRepository
{
    Task<IReadOnlyList<MessagingProvider>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>The active provider for a channel, if one is configured.</summary>
    Task<MessagingProvider?> GetActiveAsync(Guid tenantId, MessageChannel channel, CancellationToken ct = default);

    Task<MessagingProvider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(MessagingProvider provider, CancellationToken ct = default);
    Task UpdateAsync(MessagingProvider provider, CancellationToken ct = default);
    Task DeleteAsync(MessagingProvider provider, CancellationToken ct = default);
}

/// <summary>Persistence for tenant template overrides. Tenant-scoped.</summary>
public interface IMessageTemplateRepository
{
    Task<IReadOnlyList<MessageTemplate>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>The tenant's override for a key/channel/locale, if any.</summary>
    Task<MessageTemplate?> GetAsync(Guid tenantId, string key, MessageChannel channel, string locale, CancellationToken ct = default);

    Task<MessageTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(MessageTemplate template, CancellationToken ct = default);
    Task UpdateAsync(MessageTemplate template, CancellationToken ct = default);
    Task DeleteAsync(MessageTemplate template, CancellationToken ct = default);
}

/// <summary>Append-only delivery log. Tenant-scoped.</summary>
public interface IMessageLogRepository
{
    Task AddAsync(MessageLog entry, CancellationToken ct = default);
    Task<IReadOnlyList<MessageLog>> ListRecentByTenantAsync(Guid tenantId, int take, CancellationToken ct = default);
}
