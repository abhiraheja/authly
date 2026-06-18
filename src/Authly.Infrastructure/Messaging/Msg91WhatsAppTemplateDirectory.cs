using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Messaging;

/// <summary>
/// Lists the WhatsApp templates registered for a tenant's MSG91 integrated number, used by the
/// "Sync from MSG91" flow. Mirrors the proven Saar-WhatsApp integration: GET
/// <c>get-template-client/{number}</c> with the tenant's authkey, returning a per-language row.
/// </summary>
public sealed partial class Msg91WhatsAppTemplateDirectory : IWhatsAppTemplateDirectory
{
    private const string EndpointTemplate = "https://api.msg91.com/api/v5/whatsapp/get-template-client/{0}";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<Msg91WhatsAppTemplateDirectory> _logger;

    public Msg91WhatsAppTemplateDirectory(IHttpClientFactory httpClientFactory, ILogger<Msg91WhatsAppTemplateDirectory> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string Name => "msg91";

    public async Task<IReadOnlyList<WhatsAppRemoteTemplate>> ListAsync(WhatsAppProviderConfig config, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.Sender))
            throw new InvalidOperationException("MSG91 authkey and sender number are required to sync templates.");

        var endpoint = string.Format(EndpointTemplate, Uri.EscapeDataString(config.Sender));

        using var client = _httpClientFactory.CreateClient(nameof(Msg91WhatsAppTemplateDirectory));
        using var req = new HttpRequestMessage(HttpMethod.Get, endpoint);
        req.Headers.TryAddWithoutValidation("authkey", config.ApiKey);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("MSG91 get-templates failed {Status}: {Body}", resp.StatusCode, Truncate(body));
            throw new InvalidOperationException($"MSG91 returned HTTP {(int)resp.StatusCode}. {Truncate(body)}");
        }

        try
        {
            return Parse(body);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "MSG91 get-templates response could not be parsed.");
            throw new InvalidOperationException("MSG91 returned an unexpected template response.");
        }
    }

    /// <summary>
    /// Parses MSG91's response: <c>{ "data": [ { name|template_name, category,
    /// languages: [ { language, status, code: [ { type:"BODY", text } ], variables: [...] } ] } ] }</c>.
    /// One <see cref="WhatsAppRemoteTemplate"/> per (template, language).
    /// </summary>
    private static IReadOnlyList<WhatsAppRemoteTemplate> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!TryGetArray(root, "data", out var data) && !TryGetArray(root, "templates", out data))
            return Array.Empty<WhatsAppRemoteTemplate>();

        var results = new List<WhatsAppRemoteTemplate>();
        foreach (var t in data.EnumerateArray())
        {
            var name = Str(t, "name") ?? Str(t, "template_name") ?? "";
            if (name.Length == 0) continue;
            var category = Str(t, "category") ?? "UTILITY";

            if (!TryGetArray(t, "languages", out var langs)) continue;
            foreach (var lang in langs.EnumerateArray())
            {
                var language = Str(lang, "language") ?? "en";
                var status = Str(lang, "status") ?? "PENDING";

                string? bodyText = null;
                if (TryGetArray(lang, "code", out var code))
                    foreach (var c in code.EnumerateArray())
                        if (string.Equals(Str(c, "type"), "BODY", StringComparison.OrdinalIgnoreCase))
                        {
                            bodyText = Str(c, "text");
                            break;
                        }

                var varCount = TryGetArray(lang, "variables", out var vars)
                    ? vars.GetArrayLength()
                    : CountPlaceholders(bodyText);

                results.Add(new WhatsAppRemoteTemplate(name, language, status, category, bodyText, varCount));
            }
        }
        return results;
    }

    private static bool TryGetArray(JsonElement el, string name, out JsonElement array)
    {
        if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Array)
        {
            array = v;
            return true;
        }
        array = default;
        return false;
    }

    private static string? Str(JsonElement el, string name)
        => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static int CountPlaceholders(string? body)
        => string.IsNullOrEmpty(body) ? 0 : PlaceholderRegex().Matches(body).Select(m => m.Value).Distinct().Count();

    private static string Truncate(string s) => s.Length > 300 ? s[..300] : s;

    [GeneratedRegex(@"\{\{\s*\d+\s*\}\}")]
    private static partial Regex PlaceholderRegex();
}
