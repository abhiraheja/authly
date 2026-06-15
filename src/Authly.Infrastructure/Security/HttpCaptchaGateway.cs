using System.Text.Json;
using Authly.Core.Security;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Security;

/// <summary>
/// Verifies a CAPTCHA token server-side against hCaptcha or Cloudflare Turnstile (both expose a
/// compatible <c>siteverify</c> form endpoint returning <c>{"success":bool}</c>). Fails closed on
/// any error EXCEPT it returns false (caller decides) — a verification we can't complete is not a pass.
/// </summary>
public sealed class HttpCaptchaGateway : ICaptchaGateway
{
    private static readonly Dictionary<string, string> VerifyUrls = new(StringComparer.OrdinalIgnoreCase)
    {
        ["hcaptcha"] = "https://hcaptcha.com/siteverify",
        ["turnstile"] = "https://challenges.cloudflare.com/turnstile/v0/siteverify"
    };

    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<HttpCaptchaGateway> _logger;

    public HttpCaptchaGateway(IHttpClientFactory httpFactory, ILogger<HttpCaptchaGateway> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string provider, string secret, string token, string? remoteIp, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(secret)) return false;
        if (!VerifyUrls.TryGetValue(provider, out var url)) return false;

        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            var form = new Dictionary<string, string> { ["secret"] = secret, ["response"] = token };
            if (!string.IsNullOrEmpty(remoteIp)) form["remoteip"] = remoteIp;

            using var resp = await client.PostAsync(url, new FormUrlEncodedContent(form), ct);
            if (!resp.IsSuccessStatusCode) return false;

            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            return doc.RootElement.TryGetProperty("success", out var s)
                   && s.ValueKind == JsonValueKind.True;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CAPTCHA verification failed for provider {Provider}.", provider);
            return false;
        }
    }
}
