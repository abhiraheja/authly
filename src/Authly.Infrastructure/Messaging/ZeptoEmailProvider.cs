using System.Net.Http.Json;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Messaging;

/// <summary>
/// ZeptoMail (Zoho) HTTP transport. Posts to the transactional-email API with the tenant's API
/// key. Network/keys are runtime configuration, so this is exercised against a live tenant config
/// rather than in unit tests. Never throws — failures are returned for fallback/logging.
/// </summary>
public sealed class ZeptoEmailProvider : IEmailProvider
{
    private const string Endpoint = "https://api.zeptomail.com/v1.1/email";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ZeptoEmailProvider> _logger;

    public ZeptoEmailProvider(IHttpClientFactory httpClientFactory, ILogger<ZeptoEmailProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "zepto";

    public async Task<DeliveryResult> SendAsync(RenderedMessage message, EmailProviderConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.SenderEmail))
            return DeliveryResult.Fail(Name, "ZeptoMail API key and sender email are required.");

        try
        {
            var payload = new
            {
                from = new { address = config.SenderEmail, name = config.SenderName ?? config.SenderEmail },
                to = new[] { new { email_address = new { address = message.Recipient } } },
                subject = message.Subject ?? "",
                htmlbody = message.Body
            };

            using var client = _httpClientFactory.CreateClient(nameof(ZeptoEmailProvider));
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(payload) };
            // ZeptoMail expects the key verbatim in Authorization (callers store the full "Zoho-enczapikey ..." value).
            req.Headers.TryAddWithoutValidation("Authorization", config.ApiKey);

            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
                return DeliveryResult.Ok(Name);

            var body = await resp.Content.ReadAsStringAsync(ct);
            return DeliveryResult.Fail(Name, $"HTTP {(int)resp.StatusCode}: {Truncate(body)}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ZeptoMail delivery to {Recipient} failed.", message.Recipient);
            return DeliveryResult.Fail(Name, ex.Message);
        }
    }

    private static string Truncate(string s) => s.Length > 300 ? s[..300] : s;
}
