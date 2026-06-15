using System.Net;

namespace Authly.Modules.Security;

/// <summary>Pure block/allow-list matching (email domain, disposable, IP/CIDR, country).</summary>
public static class BlockListPolicy
{
    // A small built-in disposable-domain set; tenants can add more via BlockedEmailDomains.
    private static readonly HashSet<string> Disposable = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "10minutemail.com", "tempmail.com", "temp-mail.org",
        "throwawaymail.com", "yopmail.com", "trashmail.com", "getnada.com", "dispostable.com",
        "sharklasers.com", "maildrop.cc", "fakeinbox.com", "mailnesia.com"
    };

    public static bool IsEmailBlocked(string email, IEnumerable<string> blockedDomains, bool blockDisposable)
    {
        var at = email.LastIndexOf('@');
        if (at < 0 || at == email.Length - 1) return false;
        var domain = email[(at + 1)..].Trim().ToLowerInvariant();

        if (blockDisposable && Disposable.Contains(domain)) return true;
        return blockedDomains.Any(d => string.Equals(d.Trim().TrimStart('@'), domain, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>True if the IP matches any blocked single-IP or CIDR entry.</summary>
    public static bool IsIpBlocked(string? ip, IEnumerable<string> blocked)
        => ip is not null && IPAddress.TryParse(ip, out var addr) && blocked.Any(e => Matches(e, addr));

    /// <summary>True if the IP is permitted: an empty allowlist permits everything.</summary>
    public static bool IsIpAllowed(string? ip, IReadOnlyCollection<string> allowed)
    {
        if (allowed.Count == 0) return true;
        return ip is not null && IPAddress.TryParse(ip, out var addr) && allowed.Any(e => Matches(e, addr));
    }

    public static bool IsCountryBlocked(string? countryCode, IEnumerable<string> blocked)
        => !string.IsNullOrWhiteSpace(countryCode)
           && blocked.Any(c => string.Equals(c.Trim(), countryCode, StringComparison.OrdinalIgnoreCase));

    private static bool Matches(string entry, IPAddress addr)
    {
        entry = entry.Trim();
        if (entry.Contains('/'))
            return IPNetwork.TryParse(entry, out var net) && net.Contains(addr);
        return IPAddress.TryParse(entry, out var single) && single.Equals(addr);
    }
}

/// <summary>Applies a tenant's block/allow lists to emails and IPs.</summary>
public interface IBlockListService
{
    Task<bool> IsEmailBlockedAsync(Guid tenantId, string email, CancellationToken ct = default);
    Task<bool> IsIpBlockedAsync(Guid tenantId, string? ip, CancellationToken ct = default);
    Task<bool> IsIpAllowedAsync(Guid tenantId, string? ip, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class BlockListService : IBlockListService
{
    private readonly ISecuritySettingsService _settings;

    public BlockListService(ISecuritySettingsService settings) => _settings = settings;

    public async Task<bool> IsEmailBlockedAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var s = await _settings.GetAsync(tenantId, ct);
        return BlockListPolicy.IsEmailBlocked(email, s.BlockedEmailDomains, s.BlockDisposableEmails);
    }

    public async Task<bool> IsIpBlockedAsync(Guid tenantId, string? ip, CancellationToken ct = default)
    {
        var s = await _settings.GetAsync(tenantId, ct);
        return BlockListPolicy.IsIpBlocked(ip, s.BlockedIps);
    }

    public async Task<bool> IsIpAllowedAsync(Guid tenantId, string? ip, CancellationToken ct = default)
    {
        var s = await _settings.GetAsync(tenantId, ct);
        return BlockListPolicy.IsIpAllowed(ip, s.AllowedIps);
    }
}
