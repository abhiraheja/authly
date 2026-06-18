using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Messaging;
using Authly.Modules.Common;

namespace Authly.Modules.Messaging;

/// <summary>
/// Templated, provider-pluggable message delivery plus the tenant-admin configuration surface
/// (providers, template overrides, preview, send-test, delivery log). Tenant-scoped; the caller
/// binds the tenant context. <see cref="DeliverAsync"/> is invoked by the background dispatch job.
/// </summary>
public interface IMessagingService
{
    /// <summary>Resolves template + provider, renders, sends, logs, and falls back on failure.</summary>
    Task DeliverAsync(MessageSendRequest request, CancellationToken ct = default);

    // --- Providers ---
    Task<IReadOnlyList<MessagingProvider>> ListProvidersAsync(Guid tenantId, CancellationToken ct = default);
    Task<MessagingProvider?> GetProviderAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task SaveProviderAsync(Guid tenantId, ProviderConfigInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteProviderAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    // --- Templates (built-ins + tenant overrides) ---
    Task<IReadOnlyList<TemplateListItem>> ListTemplatesAsync(Guid tenantId, CancellationToken ct = default);
    Task<TemplateInput> GetTemplateForEditAsync(Guid tenantId, string key, MessageChannel channel, string locale, CancellationToken ct = default);
    Task SaveTemplateAsync(Guid tenantId, TemplateInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteTemplateAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    /// <summary>Lists the WhatsApp templates registered at the tenant's active WhatsApp provider (MSG91).</summary>
    Task<IReadOnlyList<WhatsAppRemoteTemplate>> SyncWhatsAppTemplatesAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>Binds a WhatsApp message key to an approved provider template (name + language),
    /// keeping the existing/built-in body as fallback text. Activates the override.</summary>
    Task BindWhatsAppTemplateAsync(Guid tenantId, string key, string locale, string providerTemplateName,
        string providerLanguage, AuditContext actor, CancellationToken ct = default);

    /// <summary>Renders a template against sample variables (no send).</summary>
    Task<RenderedPreview> PreviewAsync(Guid tenantId, string key, MessageChannel channel, string locale, IReadOnlyDictionary<string, string> sampleVariables, CancellationToken ct = default);

    /// <summary>Sends a real test message through the live provider for the channel.</summary>
    Task SendTestAsync(Guid tenantId, string key, MessageChannel channel, string recipient, CancellationToken ct = default);

    // --- Delivery log ---
    Task<IReadOnlyList<MessageLog>> ListRecentLogsAsync(Guid tenantId, int take = 100, CancellationToken ct = default);
}
