using Authly.Core.Messaging;

namespace Authly.Core.Interfaces;

/// <summary>
/// Transport for a specific email provider (SMTP, ZeptoMail, …). Implemented in Infrastructure;
/// selected at send time by matching <see cref="Name"/> to the tenant's configured provider key.
/// Receives a decrypted config — it must not log secrets.
/// </summary>
public interface IEmailProvider
{
    /// <summary>The provider key this transport handles (e.g. "smtp", "zepto", "log").</summary>
    string Name { get; }

    Task<DeliveryResult> SendAsync(RenderedMessage message, EmailProviderConfig config, CancellationToken ct = default);
}

/// <summary>Transport for a specific WhatsApp provider (MSG91, Gupshup, …).</summary>
public interface IWhatsAppProvider
{
    string Name { get; }

    Task<DeliveryResult> SendAsync(RenderedMessage message, WhatsAppProviderConfig config, CancellationToken ct = default);
}

/// <summary>
/// Reads the WhatsApp templates registered at the provider (MSG91), so the admin can sync and bind
/// an approved template to a message key instead of typing its name. Implemented per provider;
/// selected by matching <see cref="Name"/> to the tenant's configured WhatsApp provider key.
/// </summary>
public interface IWhatsAppTemplateDirectory
{
    string Name { get; }

    /// <summary>Lists the templates registered at the provider for the configured sender/number.</summary>
    Task<IReadOnlyList<WhatsAppRemoteTemplate>> ListAsync(WhatsAppProviderConfig config, CancellationToken ct = default);
}
