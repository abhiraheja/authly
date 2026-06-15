using System.Net.Http.Json;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Messaging;

/// <summary>
/// MSG91 WhatsApp HTTP transport. Sends the rendered body as a WhatsApp message from the tenant's
/// integrated number using their authkey. Exercised against live tenant config; never throws.
/// Gupshup follows the same shape (different endpoint/payload) and can be added alongside.
/// </summary>
public sealed class Msg91WhatsAppProvider : IWhatsAppProvider
{
    private const string Endpoint = "https://control.msg91.com/api/v5/whatsapp/whatsapp-outbound-message/";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Msg91WhatsAppProvider> _logger;

    public Msg91WhatsAppProvider(IHttpClientFactory httpClientFactory, ILogger<Msg91WhatsAppProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "msg91";

    public async Task<DeliveryResult> SendAsync(RenderedMessage message, WhatsAppProviderConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.Sender))
            return DeliveryResult.Fail(Name, "MSG91 authkey and sender number are required.");

        try
        {
            var payload = new
            {
                integrated_number = config.Sender,
                recipient_number = message.Recipient,
                content_type = "text",
                text = new { body = message.Body }
            };

            using var client = _httpClientFactory.CreateClient(nameof(Msg91WhatsAppProvider));
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(payload) };
            req.Headers.TryAddWithoutValidation("authkey", config.ApiKey);

            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
                return DeliveryResult.Ok(Name);

            var body = await resp.Content.ReadAsStringAsync(ct);
            return DeliveryResult.Fail(Name, $"HTTP {(int)resp.StatusCode}: {Truncate(body)}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MSG91 WhatsApp delivery to {Recipient} failed.", message.Recipient);
            return DeliveryResult.Fail(Name, ex.Message);
        }
    }

    private static string Truncate(string s) => s.Length > 300 ? s[..300] : s;
}
