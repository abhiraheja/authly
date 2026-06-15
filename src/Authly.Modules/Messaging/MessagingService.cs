using System.Text.Json;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.Messaging;

/// <inheritdoc />
public sealed class MessagingService : IMessagingService
{
    private const string DefaultAppName = "Authly";

    // Security-critical templates must keep the variable that carries the link/code, or the flow
    // silently breaks. Tenants may restyle the body but not remove these.
    private static readonly Dictionary<string, string> RequiredVariable = new()
    {
        [MessageTemplateKeys.VerifyEmail] = "action_url",
        [MessageTemplateKeys.ResetPassword] = "action_url",
        [MessageTemplateKeys.MagicLink] = "action_url",
        [MessageTemplateKeys.Otp] = "otp",
    };

    private readonly IMessagingProviderRepository _providers;
    private readonly IMessageTemplateRepository _templates;
    private readonly IMessageLogRepository _log;
    private readonly IEnumerable<IEmailProvider> _emailProviders;
    private readonly IEnumerable<IWhatsAppProvider> _whatsAppProviders;
    private readonly IEncryptionService _encryption;
    private readonly IAuditLogger _audit;
    private readonly ILogger<MessagingService> _logger;

    public MessagingService(
        IMessagingProviderRepository providers,
        IMessageTemplateRepository templates,
        IMessageLogRepository log,
        IEnumerable<IEmailProvider> emailProviders,
        IEnumerable<IWhatsAppProvider> whatsAppProviders,
        IEncryptionService encryption,
        IAuditLogger audit,
        ILogger<MessagingService> logger)
    {
        _providers = providers;
        _templates = templates;
        _log = log;
        _emailProviders = emailProviders;
        _whatsAppProviders = whatsAppProviders;
        _encryption = encryption;
        _audit = audit;
        _logger = logger;
    }

    // --- Delivery -----------------------------------------------------------

    public async Task DeliverAsync(MessageSendRequest request, CancellationToken ct = default)
    {
        var result = request.Channel == MessageChannel.WhatsApp
            ? await DeliverWhatsAppAsync(request, ct)
            : await DeliverEmailAsync(request, ct);

        if (!result.Success && request.Channel == MessageChannel.WhatsApp
            && request.AllowEmailFallback && !string.IsNullOrWhiteSpace(request.FallbackEmail))
        {
            _logger.LogInformation("WhatsApp delivery failed for tenant {TenantId}; falling back to email.", request.TenantId);
            var emailReq = request with
            {
                Channel = MessageChannel.Email,
                Recipient = request.FallbackEmail!,
                AllowEmailFallback = false
            };
            await DeliverEmailAsync(emailReq, ct);
        }
    }

    private async Task<DeliveryResult> DeliverEmailAsync(MessageSendRequest request, CancellationToken ct)
    {
        var content = await ResolveTemplateAsync(request.TenantId, request.TemplateKey, MessageChannel.Email, request.Locale, ct);
        if (content is null)
        {
            await LogAsync(request.TenantId, MessageChannel.Email, request.Recipient, request.TemplateKey, "failed", "no_template", ct);
            return DeliveryResult.Fail("none", "no template");
        }

        var vars = WithDefaults(request.Variables);
        var rendered = new RenderedMessage(
            MessageChannel.Email, request.Recipient,
            TemplateRenderer.Render(content.Subject ?? "", vars, htmlEncode: false),
            TemplateRenderer.Render(content.Body, vars, htmlEncode: true));

        var entity = await _providers.GetActiveAsync(request.TenantId, MessageChannel.Email, ct);
        var providerName = entity?.Provider ?? "log";
        var transport = _emailProviders.FirstOrDefault(p => p.Name == providerName)
                        ?? _emailProviders.First(p => p.Name == "log");
        var config = ResolveEmailConfig(entity);

        var result = await transport.SendAsync(rendered, config, ct);
        await LogAsync(request.TenantId, MessageChannel.Email, request.Recipient, request.TemplateKey,
            result.Success ? "sent" : "failed", result.Error, ct);
        return result;
    }

    private async Task<DeliveryResult> DeliverWhatsAppAsync(MessageSendRequest request, CancellationToken ct)
    {
        var content = await ResolveTemplateAsync(request.TenantId, request.TemplateKey, MessageChannel.WhatsApp, request.Locale, ct);
        if (content is null)
        {
            await LogAsync(request.TenantId, MessageChannel.WhatsApp, request.Recipient, request.TemplateKey, "failed", "no_template", ct);
            return DeliveryResult.Fail("none", "no template");
        }

        var vars = WithDefaults(request.Variables);
        var rendered = new RenderedMessage(MessageChannel.WhatsApp, request.Recipient, null,
            TemplateRenderer.Render(content.Body, vars, htmlEncode: false));

        var entity = await _providers.GetActiveAsync(request.TenantId, MessageChannel.WhatsApp, ct);
        var providerName = entity?.Provider ?? "log";
        var transport = _whatsAppProviders.FirstOrDefault(p => p.Name == providerName)
                        ?? _whatsAppProviders.First(p => p.Name == "log");
        var config = ResolveWhatsAppConfig(entity);

        var result = await transport.SendAsync(rendered, config, ct);
        await LogAsync(request.TenantId, MessageChannel.WhatsApp, request.Recipient, request.TemplateKey,
            result.Success ? "sent" : "failed", result.Error, ct);
        return result;
    }

    // --- Providers ----------------------------------------------------------

    public Task<IReadOnlyList<MessagingProvider>> ListProvidersAsync(Guid tenantId, CancellationToken ct = default)
        => _providers.ListByTenantAsync(tenantId, ct);

    public Task<MessagingProvider?> GetProviderAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _providers.GetByIdAsync(tenantId, id, ct);

    public async Task SaveProviderAsync(Guid tenantId, ProviderConfigInput input, AuditContext actor, CancellationToken ct = default)
    {
        var existing = input.Id is { } id ? await _providers.GetByIdAsync(tenantId, id, ct) : null;
        var configJson = input.Channel == MessageChannel.WhatsApp
            ? BuildWhatsAppConfig(input, existing?.Config)
            : BuildEmailConfig(input, existing?.Config);

        if (existing is null)
        {
            await _providers.AddAsync(new MessagingProvider
            {
                TenantId = tenantId,
                Channel = input.Channel,
                Provider = input.Provider,
                Mode = input.Mode,
                Config = configJson,
                IsActive = input.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
        }
        else
        {
            existing.Provider = input.Provider;
            existing.Mode = input.Mode;
            existing.Config = configJson;
            existing.IsActive = input.IsActive;
            await _providers.UpdateAsync(existing, ct);
        }

        await _audit.LogAsync("messaging.provider_saved", actor, tenantId, "messaging_provider", existing?.Id,
            metadata: new { input.Channel, input.Provider, input.IsActive }, ct: ct);
    }

    public async Task DeleteProviderAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var entity = await _providers.GetByIdAsync(tenantId, id, ct);
        if (entity is null) return;
        await _providers.DeleteAsync(entity, ct);
        await _audit.LogAsync("messaging.provider_deleted", actor, tenantId, "messaging_provider", id, ct: ct);
    }

    // --- Templates ----------------------------------------------------------

    public async Task<IReadOnlyList<TemplateListItem>> ListTemplatesAsync(Guid tenantId, CancellationToken ct = default)
    {
        var overrides = await _templates.ListByTenantAsync(tenantId, ct);
        var rows = new List<TemplateListItem>();

        foreach (var (key, channel, content) in BuiltInTemplates.All)
        {
            var ov = overrides.FirstOrDefault(o => o.Key == key && o.Channel == channel && o.Locale == "en");
            rows.Add(new TemplateListItem(ov?.Id, key, channel, "en",
                ov?.Subject ?? content.Subject, ov is not null, ov?.IsActive ?? true));
        }

        // Tenant overrides that aren't an "en" built-in (extra locales / custom keys).
        foreach (var ov in overrides)
        {
            var isBuiltinEn = ov.Locale == "en" && BuiltInTemplates.Find(ov.Key, ov.Channel) is not null;
            if (!isBuiltinEn)
                rows.Add(new TemplateListItem(ov.Id, ov.Key, ov.Channel, ov.Locale, ov.Subject, true, ov.IsActive));
        }

        return rows;
    }

    public async Task<TemplateInput> GetTemplateForEditAsync(Guid tenantId, string key, MessageChannel channel, string locale, CancellationToken ct = default)
    {
        var ov = await _templates.GetAsync(tenantId, key, channel, locale, ct);
        if (ov is not null)
            return new TemplateInput { Id = ov.Id, Key = ov.Key, Channel = ov.Channel, Locale = ov.Locale, Subject = ov.Subject, Body = ov.Body, IsActive = ov.IsActive };

        var content = BuiltInTemplates.Find(key, channel)
            ?? throw new TemplateNotFoundException(key, channel);
        return new TemplateInput { Id = null, Key = key, Channel = channel, Locale = locale, Subject = content.Subject, Body = content.Body, IsActive = true };
    }

    public async Task SaveTemplateAsync(Guid tenantId, TemplateInput input, AuditContext actor, CancellationToken ct = default)
    {
        ValidateRequiredVariable(input);

        var existing = input.Id is { } id
            ? await _templates.GetByIdAsync(tenantId, id, ct)
            : await _templates.GetAsync(tenantId, input.Key, input.Channel, input.Locale, ct);

        if (existing is null)
        {
            await _templates.AddAsync(new MessageTemplate
            {
                TenantId = tenantId,
                Key = input.Key,
                Channel = input.Channel,
                Locale = input.Locale,
                Subject = input.Subject,
                Body = input.Body,
                IsActive = input.IsActive,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
        }
        else
        {
            existing.Subject = input.Subject;
            existing.Body = input.Body;
            existing.IsActive = input.IsActive;
            await _templates.UpdateAsync(existing, ct);
        }

        await _audit.LogAsync("messaging.template_saved", actor, tenantId, "message_template", existing?.Id,
            metadata: new { input.Key, input.Channel, input.Locale }, ct: ct);
    }

    public async Task DeleteTemplateAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var entity = await _templates.GetByIdAsync(tenantId, id, ct);
        if (entity is null) return;
        await _templates.DeleteAsync(entity, ct);
        await _audit.LogAsync("messaging.template_deleted", actor, tenantId, "message_template", id,
            metadata: new { entity.Key, entity.Channel, entity.Locale }, ct: ct);
    }

    public async Task<RenderedPreview> PreviewAsync(Guid tenantId, string key, MessageChannel channel, string locale, IReadOnlyDictionary<string, string> sampleVariables, CancellationToken ct = default)
    {
        var content = await ResolveTemplateAsync(tenantId, key, channel, locale, ct)
            ?? throw new TemplateNotFoundException(key, channel);
        var vars = WithDefaults(sampleVariables);
        var html = channel == MessageChannel.Email;
        return new RenderedPreview(channel,
            content.Subject is null ? null : TemplateRenderer.Render(content.Subject, vars, htmlEncode: false),
            TemplateRenderer.Render(content.Body, vars, htmlEncode: html));
    }

    public Task SendTestAsync(Guid tenantId, string key, MessageChannel channel, string recipient, CancellationToken ct = default)
    {
        var sample = SampleVariables();
        return DeliverAsync(new MessageSendRequest(tenantId, key, channel, recipient, sample), ct);
    }

    public Task<IReadOnlyList<MessageLog>> ListRecentLogsAsync(Guid tenantId, int take = 100, CancellationToken ct = default)
        => _log.ListRecentByTenantAsync(tenantId, take, ct);

    // --- helpers ------------------------------------------------------------

    private async Task<TemplateContent?> ResolveTemplateAsync(Guid tenantId, string key, MessageChannel channel, string locale, CancellationToken ct)
    {
        var ov = await _templates.GetAsync(tenantId, key, channel, locale, ct);
        if (ov is null && !string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase))
            ov = await _templates.GetAsync(tenantId, key, channel, "en", ct);

        if (ov is not null)
            return new TemplateContent(ov.Subject, ov.Body);

        return BuiltInTemplates.Find(key, channel);
    }

    private static void ValidateRequiredVariable(TemplateInput input)
    {
        if (RequiredVariable.TryGetValue(input.Key, out var required)
            && !TemplateRenderer.Placeholders(input.Body).Contains(required))
        {
            throw new TemplateValidationException(
                $"The '{input.Key}' template must include the {{{{{required}}}}} variable so the flow still works.");
        }
    }

    private EmailProviderConfig ResolveEmailConfig(MessagingProvider? entity)
    {
        if (entity is null)
            return new EmailProviderConfig { SenderEmail = "no-reply@localhost", SenderName = DefaultAppName };

        var cfg = Deserialize<EmailProviderConfig>(entity.Config) ?? new EmailProviderConfig();
        cfg.ApiKey = Decrypt(cfg.ApiKey);
        cfg.Password = Decrypt(cfg.Password);
        return cfg;
    }

    private WhatsAppProviderConfig ResolveWhatsAppConfig(MessagingProvider? entity)
    {
        if (entity is null)
            return new WhatsAppProviderConfig();

        var cfg = Deserialize<WhatsAppProviderConfig>(entity.Config) ?? new WhatsAppProviderConfig();
        cfg.ApiKey = Decrypt(cfg.ApiKey);
        return cfg;
    }

    private string BuildEmailConfig(ProviderConfigInput input, string? existingJson)
    {
        var existing = existingJson is null ? null : Deserialize<EmailProviderConfig>(existingJson);
        var cfg = new EmailProviderConfig
        {
            SenderEmail = input.SenderEmail,
            SenderName = input.SenderName,
            Host = input.Host,
            Port = input.Port,
            UseSsl = input.UseSsl,
            Username = input.Username,
            // Secrets: encrypt new value, else keep the already-encrypted stored value.
            ApiKey = string.IsNullOrWhiteSpace(input.ApiKey) ? existing?.ApiKey : _encryption.Encrypt(input.ApiKey),
            Password = string.IsNullOrWhiteSpace(input.Password) ? existing?.Password : _encryption.Encrypt(input.Password)
        };
        return JsonSerializer.Serialize(cfg);
    }

    private string BuildWhatsAppConfig(ProviderConfigInput input, string? existingJson)
    {
        var existing = existingJson is null ? null : Deserialize<WhatsAppProviderConfig>(existingJson);
        var cfg = new WhatsAppProviderConfig
        {
            Sender = input.Sender,
            AccountId = input.AccountId,
            ApiKey = string.IsNullOrWhiteSpace(input.ApiKey) ? existing?.ApiKey : _encryption.Encrypt(input.ApiKey)
        };
        return JsonSerializer.Serialize(cfg);
    }

    private string? Decrypt(string? cipher)
    {
        if (string.IsNullOrEmpty(cipher)) return cipher;
        try { return _encryption.Decrypt(cipher); }
        catch (Exception) { return null; } // corrupt/rotated key — treat as unset rather than crash a send
    }

    private async Task LogAsync(Guid tenantId, MessageChannel channel, string recipient, string? key, string status, string? error, CancellationToken ct)
    {
        try
        {
            await _log.AddAsync(new MessageLog
            {
                TenantId = tenantId,
                Channel = channel,
                Recipient = recipient,
                TemplateKey = key,
                Status = status,
                Error = error,
                CreatedAt = DateTimeOffset.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write message_log entry for tenant {TenantId}.", tenantId);
        }
    }

    private static Dictionary<string, string> WithDefaults(IReadOnlyDictionary<string, string> vars)
    {
        var merged = new Dictionary<string, string>(vars);
        merged.TryAdd("app_name", DefaultAppName);
        merged.TryAdd("user_name", "there");
        return merged;
    }

    private static Dictionary<string, string> SampleVariables() => new()
    {
        ["app_name"] = DefaultAppName,
        ["user_name"] = "Sample User",
        ["action_url"] = "https://example.com/verify?token=sample",
        ["otp"] = "123456",
        ["expiry_hours"] = "24",
        ["expiry_minutes"] = "10",
        ["message"] = "This is a sample security alert."
    };

    private static T? Deserialize<T>(string json) where T : class
    {
        try { return JsonSerializer.Deserialize<T>(json); }
        catch (JsonException) { return null; }
    }
}
