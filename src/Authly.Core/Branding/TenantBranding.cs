using Authly.Core.Enums;

namespace Authly.Core.Branding;

/// <summary>
/// A tenant's visual identity for the hosted login/register/MFA pages and the end-user portal.
/// Persisted as the JSON in <c>tenants.branding</c>; <see cref="Default"/> supplies the platform
/// look when a tenant has not customized anything. This is a plain value object — parsing,
/// validation, and persistence live in the Branding module service.
/// </summary>
public sealed class TenantBranding
{
    /// <summary>The platform default (Authly indigo, Inter, centered, light).</summary>
    public static TenantBranding Default => new();

    /// <summary>Absolute https(s) URL of the tenant's logo. Null falls back to the shield mark + name.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>Primary brand color as a CSS hex (e.g. <c>#5b6df5</c>). Drives buttons, links, accents.</summary>
    public string PrimaryColor { get; set; } = "#5b6df5";

    /// <summary>Text color rendered on top of <see cref="PrimaryColor"/> (button labels). Hex.</summary>
    public string ButtonTextColor { get; set; } = "#ffffff";

    /// <summary>CSS font-family stack for the hosted pages. The platform uses Inter by default.</summary>
    public string FontFamily { get; set; } = "Inter, system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif";

    /// <summary>Layout of the hosted pages.</summary>
    public BrandingLayout Layout { get; set; } = BrandingLayout.Centered;

    /// <summary>When true the hosted pages default to dark mode.</summary>
    public bool DarkMode { get; set; }

    /// <summary>Optional one-line tagline shown beneath the brand on split layouts.</summary>
    public string? Tagline { get; set; }

    /// <summary>
    /// <see cref="PrimaryColor"/> expressed as a CSS <c>r, g, b</c> triplet for Bootstrap's
    /// <c>--bs-primary-rgb</c> variable (used in rgba() soft backgrounds). Falls back to the
    /// platform indigo if the hex can't be parsed.
    /// </summary>
    public string PrimaryColorRgb => HexToRgb(PrimaryColor) ?? "91, 109, 245";

    private static string? HexToRgb(string hex)
    {
        var h = hex.TrimStart('#');
        if (h.Length == 3) h = string.Concat(h[0], h[0], h[1], h[1], h[2], h[2]);
        if (h.Length != 6
            || !int.TryParse(h.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)
            || !int.TryParse(h.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)
            || !int.TryParse(h.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return null;
        return $"{r}, {g}, {b}";
    }

    /// <summary>True when nothing has been customized away from the platform default.</summary>
    public bool IsDefault =>
        LogoUrl is null
        && PrimaryColor == Default.PrimaryColor
        && ButtonTextColor == Default.ButtonTextColor
        && FontFamily == Default.FontFamily
        && Layout == BrandingLayout.Centered
        && !DarkMode
        && string.IsNullOrEmpty(Tagline);
}
