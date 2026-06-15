using System.Security.Cryptography;
using System.Text;
using Authly.Core.Security;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Security;

/// <summary>HTTP client for the HaveIBeenPwned range API (k-anonymity). Never throws — returns null on failure.</summary>
public sealed class PwnedRangeClient : IPwnedRangeClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<PwnedRangeClient> _logger;

    public PwnedRangeClient(IHttpClientFactory httpFactory, ILogger<PwnedRangeClient> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<string?> GetRangeAsync(string hashPrefix, CancellationToken ct = default)
    {
        try
        {
            var client = _httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://api.pwnedpasswords.com/range/{hashPrefix}");
            req.Headers.Add("Add-Padding", "true"); // hide the real bucket size
            req.Headers.UserAgent.ParseAdd("Authly-IDaaS");

            using var resp = await client.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            // Breach checking must never block sign-up if the service is down.
            _logger.LogWarning(ex, "Pwned range lookup failed; treating password as not-breached.");
            return null;
        }
    }
}

/// <summary>
/// Breached-password check via HIBP k-anonymity: SHA-1 the password, send only the first 5 hex
/// chars, and match the returned suffixes locally. The matching is here (not in the HTTP client)
/// so it can be unit-tested with a fake range client.
/// </summary>
public sealed class HibpBreachedPasswordGateway : IBreachedPasswordGateway
{
    private readonly IPwnedRangeClient _range;

    public HibpBreachedPasswordGateway(IPwnedRangeClient range) => _range = range;

    public async Task<bool> IsBreachedAsync(string password, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(password)) return false;

        var sha1 = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));
        var prefix = sha1[..5];
        var suffix = sha1[5..];

        var body = await _range.GetRangeAsync(prefix, ct);
        if (body is null) return false; // fail open

        foreach (var line in body.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            // Match the suffix; a count of 0 (padding row) means "not actually breached".
            if (line.AsSpan(0, colon).Trim().Equals(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var countPart = line.AsSpan(colon + 1).Trim();
                return !(int.TryParse(countPart, out var count) && count == 0);
            }
        }
        return false;
    }
}
