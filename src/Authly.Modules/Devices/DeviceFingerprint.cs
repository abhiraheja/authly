using System.Security.Cryptography;
using System.Text;

namespace Authly.Modules.Devices;

/// <summary>
/// Derives a stable, non-secret device fingerprint and a friendly label from the user-agent.
/// This is a server-side approximation (no client-side fingerprinting) — good enough to group a
/// user's sessions into "devices" and to remember trusted ones.
/// </summary>
public static class DeviceFingerprint
{
    /// <summary>SHA-256 hex of the normalized user-agent, or "unknown" when absent.</summary>
    public static string From(string? userAgent)
    {
        var ua = (userAgent ?? "").Trim();
        if (ua.Length == 0) return "unknown";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ua));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>A human label like "Chrome on Windows" parsed loosely from the user-agent.</summary>
    public static string Label(string? userAgent)
    {
        var ua = userAgent ?? "";
        var browser =
            ua.Contains("Edg", StringComparison.OrdinalIgnoreCase) ? "Edge" :
            ua.Contains("OPR", StringComparison.OrdinalIgnoreCase) ? "Opera" :
            ua.Contains("Firefox", StringComparison.OrdinalIgnoreCase) ? "Firefox" :
            ua.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ? "Chrome" :
            ua.Contains("Safari", StringComparison.OrdinalIgnoreCase) ? "Safari" :
            "Browser";
        var os =
            ua.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows" :
            ua.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) || ua.Contains("Macintosh", StringComparison.OrdinalIgnoreCase) ? "macOS" :
            ua.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android" :
            ua.Contains("iPhone", StringComparison.OrdinalIgnoreCase) || ua.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iOS" :
            ua.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux" :
            "Unknown OS";
        return $"{browser} on {os}";
    }
}
