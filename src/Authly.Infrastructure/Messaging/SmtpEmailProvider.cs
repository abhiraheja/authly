using System.Net;
using System.Net.Mail;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Messaging;

/// <summary>
/// SMTP email transport (self-host / any SMTP relay) via <see cref="SmtpClient"/>. Config supplies
/// host/port/credentials and the sender; the rendered body is sent as HTML with a stripped-text
/// alternate. Returns a failure result (never throws) so the dispatcher can fall back / log.
/// </summary>
public sealed class SmtpEmailProvider : IEmailProvider
{
    private readonly ILogger<SmtpEmailProvider> _logger;
    public SmtpEmailProvider(ILogger<SmtpEmailProvider> logger) => _logger = logger;

    public string Name => "smtp";

    public async Task<DeliveryResult> SendAsync(RenderedMessage message, EmailProviderConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.Host) || string.IsNullOrWhiteSpace(config.SenderEmail))
            return DeliveryResult.Fail(Name, "SMTP host and sender email are required.");

        try
        {
            using var client = new SmtpClient(config.Host, config.Port ?? 587)
            {
                EnableSsl = config.UseSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network
            };
            if (!string.IsNullOrEmpty(config.Username))
                client.Credentials = new NetworkCredential(config.Username, config.Password);

            using var mail = new MailMessage
            {
                From = new MailAddress(config.SenderEmail, config.SenderName ?? config.SenderEmail),
                Subject = message.Subject ?? "",
                Body = message.Body,
                IsBodyHtml = true
            };
            mail.To.Add(message.Recipient);

            await client.SendMailAsync(mail, ct);
            return DeliveryResult.Ok(Name);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SMTP delivery to {Recipient} failed.", message.Recipient);
            return DeliveryResult.Fail(Name, ex.Message);
        }
    }
}
