using Authly.Core.Enums;

namespace Authly.Modules.Messaging;

/// <summary>Admin input for creating/updating a tenant's provider config. Blank secrets = keep existing.</summary>
public sealed class ProviderConfigInput
{
    public Guid? Id { get; set; }
    public MessageChannel Channel { get; set; }
    public string Provider { get; set; } = default!;
    public string Mode { get; set; } = "byok";
    public bool IsActive { get; set; } = true;

    // Email fields
    public string? SenderEmail { get; set; }
    public string? SenderName { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public bool UseSsl { get; set; } = true;
    public string? Username { get; set; }

    // WhatsApp fields
    public string? Sender { get; set; }
    public string? AccountId { get; set; }

    // Secrets (write-only): null/blank means "leave the stored value unchanged".
    public string? ApiKey { get; set; }
    public string? Password { get; set; }
}

/// <summary>A template row for the admin list — either a tenant override or a built-in default.</summary>
public sealed record TemplateListItem(
    Guid? Id, string Key, MessageChannel Channel, string Locale, string? Subject, bool IsOverride, bool IsActive);

/// <summary>Editable template content (override of a built-in, or a new locale).</summary>
public sealed class TemplateInput
{
    public Guid? Id { get; set; }
    public string Key { get; set; } = default!;
    public MessageChannel Channel { get; set; }
    public string Locale { get; set; } = "en";
    public string? Subject { get; set; }
    public string Body { get; set; } = default!;
    public bool IsActive { get; set; } = true;

    /// <summary>WhatsApp only: the approved provider template name this key is bound to.</summary>
    public string? ProviderTemplateName { get; set; }

    /// <summary>WhatsApp only: the approved template's language code (e.g. "en", "en_US").</summary>
    public string? ProviderLanguage { get; set; }
}

/// <summary>Result of rendering a template against sample/real variables (for preview + send-test).</summary>
public sealed record RenderedPreview(MessageChannel Channel, string? Subject, string Body);

public sealed class TemplateNotFoundException : Exception
{
    public TemplateNotFoundException(string key, MessageChannel channel)
        : base($"No template found for '{key}' on {channel}.") { }
}

/// <summary>Thrown when a security-critical template omits a required variable (e.g. {{action_url}}).</summary>
public sealed class TemplateValidationException : Exception
{
    public TemplateValidationException(string message) : base(message) { }
}
