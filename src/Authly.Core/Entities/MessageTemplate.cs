using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A tenant-customized message template for a given key/channel/locale. Absent a tenant row, the
/// platform's built-in default for that key is used. Body carries <c>{{variables}}</c>. Unique
/// per (tenant, key, channel, locale). Maps to table "message_templates".
/// </summary>
public class MessageTemplate
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>verify_email | reset_password | otp | magic_link | welcome | security_alert | …</summary>
    public string Key { get; set; } = default!;

    public MessageChannel Channel { get; set; }
    public string Locale { get; set; } = "en";

    /// <summary>Email subject (null for WhatsApp).</summary>
    public string? Subject { get; set; }

    /// <summary>Body with <c>{{variable}}</c> placeholders (HTML for email).</summary>
    public string Body { get; set; } = default!;

    /// <summary>
    /// WhatsApp only: the provider-approved template name (e.g. "authly_otp") this key is bound to.
    /// When set, the dispatcher sends a template message (positional <c>{{1}}…</c> params from the
    /// key's <see cref="Authly.Core.Enums.MessageChannel.WhatsApp"/> spec) instead of free text.
    /// Null for email and for unbound WhatsApp templates.
    /// </summary>
    public string? ProviderTemplateName { get; set; }

    /// <summary>WhatsApp only: the approved template's language code (e.g. "en", "en_US").</summary>
    public string? ProviderLanguage { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
