using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Authly.Core.OAuth;

namespace Authly.Infrastructure.OAuth;

/// <summary>
/// OAuth2 authorization-code transport over <see cref="HttpClient"/>. Provider-agnostic: it
/// drives any standard endpoints supplied in <see cref="SocialAuthConfig"/> (the module layer
/// fills those from presets/config and maps the returned user-info JSON). Live handshakes require
/// real client credentials + network, so this is exercised against configured providers.
/// </summary>
public sealed class SocialAuthGateway : ISocialAuthGateway
{
    private readonly IHttpClientFactory _httpClientFactory;

    public SocialAuthGateway(IHttpClientFactory httpClientFactory) => _httpClientFactory = httpClientFactory;

    public string BuildAuthorizationUrl(SocialAuthConfig config, string redirectUri, string state)
    {
        var query = new (string Key, string Value)[]
        {
            ("response_type", "code"),
            ("client_id", config.ClientId),
            ("redirect_uri", redirectUri),
            ("scope", string.Join(' ', config.Scopes)),
            ("state", state),
            // Request a refresh token from providers that gate it behind these (Google/Microsoft).
            ("access_type", "offline"),
            ("prompt", "consent")
        };
        var sb = new StringBuilder(config.AuthorizationEndpoint);
        sb.Append(config.AuthorizationEndpoint.Contains('?') ? '&' : '?');
        for (var i = 0; i < query.Length; i++)
        {
            if (i > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(query[i].Key)).Append('=').Append(Uri.EscapeDataString(query[i].Value));
        }
        return sb.ToString();
    }

    public async Task<SocialTokenSet> ExchangeCodeAsync(SocialAuthConfig config, string code, string redirectUri, CancellationToken ct = default)
    {
        using var client = _httpClientFactory.CreateClient(nameof(SocialAuthGateway));
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = config.ClientId,
            ["client_secret"] = config.ClientSecret
        });
        using var req = new HttpRequestMessage(HttpMethod.Post, config.TokenEndpoint) { Content = content };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new SocialAuthException($"Token exchange failed ({(int)resp.StatusCode}).");

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var accessToken = GetString(root, "access_token")
            ?? throw new SocialAuthException("Token response had no access_token.");
        var refreshToken = GetString(root, "refresh_token");
        var idToken = GetString(root, "id_token");
        DateTimeOffset? expiresAt = root.TryGetProperty("expires_in", out var exp) && exp.TryGetInt32(out var seconds)
            ? DateTimeOffset.UtcNow.AddSeconds(seconds)
            : null;

        return new SocialTokenSet(accessToken, refreshToken, expiresAt, idToken);
    }

    public async Task<string> FetchUserInfoJsonAsync(SocialAuthConfig config, SocialTokenSet tokens, CancellationToken ct = default)
    {
        using var client = _httpClientFactory.CreateClient(nameof(SocialAuthGateway));
        using var req = new HttpRequestMessage(HttpMethod.Get, config.UserInfoEndpoint);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        // Some providers (GitHub) reject requests without a User-Agent.
        req.Headers.UserAgent.ParseAdd("Authly/1.0");

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new SocialAuthException($"User-info fetch failed ({(int)resp.StatusCode}).");
        return body;
    }

    private static string? GetString(JsonElement root, string name)
        => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
}
