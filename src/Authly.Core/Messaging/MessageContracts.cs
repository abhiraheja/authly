using Authly.Core.Enums;

namespace Authly.Core.Messaging;

/// <summary>
/// A request to deliver a templated message. Built by module services and enqueued; the
/// dispatcher resolves the template + provider, renders variables, sends, logs, and (optionally)
/// falls back to email. Carries no rendered body — only the variables needed to render one.
/// </summary>
/// <param name="TenantId">Owning tenant (selects provider config + template overrides).</param>
/// <param name="TemplateKey">e.g. <c>verify_email</c>, <c>otp</c>.</param>
/// <param name="Channel">Preferred channel.</param>
/// <param name="Recipient">Email address or E.164 phone number.</param>
/// <param name="Variables">Values substituted into <c>{{placeholders}}</c>.</param>
/// <param name="Locale">Template locale; falls back to <c>en</c>.</param>
/// <param name="AllowEmailFallback">If a WhatsApp send fails, retry on email.</param>
/// <param name="FallbackEmail">Email to use when falling back from WhatsApp.</param>
public sealed record MessageSendRequest(
    Guid TenantId,
    string TemplateKey,
    MessageChannel Channel,
    string Recipient,
    IReadOnlyDictionary<string, string> Variables,
    string Locale = "en",
    bool AllowEmailFallback = false,
    string? FallbackEmail = null);

/// <summary>One named body parameter of a WhatsApp template (<c>{{otp}}</c> → "123456").</summary>
public sealed record WhatsAppNamedParam(string Name, string Value);

/// <summary>A fully rendered message handed to a channel provider for transport.</summary>
/// <param name="WhatsAppTemplateName">WhatsApp only: when set, the provider sends a pre-approved
/// template message by this name (with the body params below) instead of the free-text
/// <paramref name="Body"/>. Null → free text.</param>
/// <param name="WhatsAppLanguage">Language code of the approved template (e.g. "en", "en_US").</param>
/// <param name="WhatsAppParameters">Legacy positional values for <c>{{1}}…{{n}}</c> body params.</param>
/// <param name="WhatsAppNamedParameters">Ordered named body params (<c>{{name}}</c> templates).
/// When set, takes precedence over <paramref name="WhatsAppParameters"/>.</param>
public sealed record RenderedMessage(
    MessageChannel Channel,
    string Recipient,
    string? Subject,
    string Body,
    string? WhatsAppTemplateName = null,
    string? WhatsAppLanguage = null,
    IReadOnlyList<string>? WhatsAppParameters = null,
    IReadOnlyList<WhatsAppNamedParam>? WhatsAppNamedParameters = null);

/// <summary>
/// A WhatsApp template as it exists at the provider (MSG91), surfaced for the sync + bind UI.
/// <paramref name="VariableCount"/> is the number of body params; <paramref name="VariableNames"/>
/// holds the parsed <c>{{name}}</c> tokens (named templates) when available.
/// </summary>
public sealed record WhatsAppRemoteTemplate(
    string Name,
    string Language,
    string Status,
    string Category,
    string? BodyText,
    int VariableCount,
    IReadOnlyList<string>? VariableNames = null);

/// <summary>Outcome of a single provider send attempt.</summary>
public sealed record DeliveryResult(bool Success, string ProviderName, string? Error = null)
{
    public static DeliveryResult Ok(string provider) => new(true, provider);
    public static DeliveryResult Fail(string provider, string error) => new(false, provider, error);
}

/// <summary>
/// Email provider connection settings (BYOK). Secret fields (<see cref="ApiKey"/>,
/// <see cref="Password"/>) are encrypted at rest and decrypted only at send time.
/// </summary>
public sealed class EmailProviderConfig
{
    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; }

    // SMTP
    public string? Host { get; set; }
    public int? Port { get; set; }
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }
    public string? Password { get; set; }

    // HTTP API providers (Zepto/SendGrid/…)
    public string? ApiKey { get; set; }
}

/// <summary>WhatsApp provider connection settings (BYOK). <see cref="ApiKey"/> is encrypted at rest.</summary>
public sealed class WhatsAppProviderConfig
{
    public string? ApiKey { get; set; }

    /// <summary>Sender/WABA phone number or sender id.</summary>
    public string? Sender { get; set; }

    /// <summary>Provider-specific account/namespace identifier (e.g. MSG91 integrated number).</summary>
    public string? AccountId { get; set; }
}
