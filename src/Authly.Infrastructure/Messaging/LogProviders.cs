using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Messaging;

/// <summary>
/// Development email transport: logs the recipient/subject at Information and the body (which may
/// contain one-time links/codes) only at Debug. Used when a tenant has not configured a real
/// provider, so flows still "deliver" locally. Replaces the Phase 2 StubEmailSender.
/// </summary>
public sealed class LogEmailProvider : IEmailProvider
{
    private readonly ILogger<LogEmailProvider> _logger;
    public LogEmailProvider(ILogger<LogEmailProvider> logger) => _logger = logger;

    public string Name => "log";

    public Task<DeliveryResult> SendAsync(RenderedMessage message, EmailProviderConfig config, CancellationToken ct = default)
    {
        _logger.LogInformation("[LOG EMAIL] To {Recipient} | Subject: {Subject} (no real provider configured)",
            message.Recipient, message.Subject);
        _logger.LogDebug("[LOG EMAIL] Body for {Recipient}:\n{Body}", message.Recipient, message.Body);
        return Task.FromResult(DeliveryResult.Ok(Name));
    }
}

/// <summary>Development WhatsApp transport: logs metadata at Information, body only at Debug.</summary>
public sealed class LogWhatsAppProvider : IWhatsAppProvider
{
    private readonly ILogger<LogWhatsAppProvider> _logger;
    public LogWhatsAppProvider(ILogger<LogWhatsAppProvider> logger) => _logger = logger;

    public string Name => "log";

    public Task<DeliveryResult> SendAsync(RenderedMessage message, WhatsAppProviderConfig config, CancellationToken ct = default)
    {
        _logger.LogInformation("[LOG WHATSAPP] To {Recipient} (no real provider configured)", message.Recipient);
        _logger.LogDebug("[LOG WHATSAPP] Body for {Recipient}:\n{Body}", message.Recipient, message.Body);
        return Task.FromResult(DeliveryResult.Ok(Name));
    }
}
