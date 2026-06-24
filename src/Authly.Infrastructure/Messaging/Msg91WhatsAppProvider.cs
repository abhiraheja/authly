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
    // Bulk endpoint — matches the to_and_components payload shape used below.
    private const string Endpoint = "https://api.msg91.com/api/v5/whatsapp/whatsapp-outbound-message/bulk/";

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
            object payload;
            if (!string.IsNullOrWhiteSpace(message.WhatsAppTemplateName))
            {
                // Template message (required for business-initiated sends like OTP). Shape mirrors the
                // working Saar-WhatsApp MSG91 integration: `components` is an OBJECT keyed by the
                // template's component name (body_1, body_otp, …) with {type:"text", value}; `to` is an
                // array of recipients; `language` is an object; `messaging_product` is included.
                // A button_N param is a dynamic URL button (auth copy-code) and needs sub_type:"url".
                var components = new Dictionary<string, object>();
                if (message.WhatsAppNamedParameters is { Count: > 0 } named)
                {
                    foreach (var p in named)
                        components[ComponentKey(p.Name)] = BuildComponent(p.Name, p.Value);
                }
                else
                {
                    var positional = message.WhatsAppParameters ?? Array.Empty<string>();
                    for (var i = 0; i < positional.Count; i++)
                        components[$"body_{i + 1}"] = new { type = "text", value = positional[i] };
                }

                var template = new Dictionary<string, object>
                {
                    ["name"] = message.WhatsAppTemplateName,
                    ["language"] = new { code = message.WhatsAppLanguage ?? "en", policy = "deterministic" },
                    ["to_and_components"] = new[] { new { to = new[] { message.Recipient }, components } }
                };
                // WABA namespace (config "Account / namespace id"), required by some MSG91 accounts.
                if (!string.IsNullOrWhiteSpace(config.AccountId))
                    template["namespace"] = config.AccountId;

                payload = new
                {
                    integrated_number = config.Sender,
                    content_type = "template",
                    payload = new
                    {
                        messaging_product = "whatsapp",
                        type = "template",
                        template
                    }
                };
            }
            else
            {
                // Free text — only valid inside WhatsApp's 24-hour service window (or the log provider).
                payload = new
                {
                    integrated_number = config.Sender,
                    recipient_number = message.Recipient,
                    content_type = "text",
                    text = new { body = message.Body }
                };
            }

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

    /// <summary>The MSG91 components-map key: a bare numeric body index ("1") becomes "body_1";
    /// provider-prefixed names (body_otp, button_1, …) are used as-is.</summary>
    private static string ComponentKey(string name)
        => int.TryParse(name, out var n) ? $"body_{n}" : name;

    /// <summary>Builds a component value. Button params are dynamic URL buttons (auth copy-code) and
    /// MSG91 requires a <c>subtype</c> field (its error calls it "sub_type", but the accepted payload
    /// key is <c>subtype</c>); body and header params are plain text.</summary>
    private static object BuildComponent(string name, string value)
        => name.StartsWith("button_", StringComparison.OrdinalIgnoreCase)
            ? new { type = "text", subtype = "url", value }
            : new { type = "text", value };

    private static string Truncate(string s) => s.Length > 300 ? s[..300] : s;
}
