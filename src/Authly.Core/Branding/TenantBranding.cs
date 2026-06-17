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

    // --- identity -----------------------------------------------------------

    /// <summary>Absolute https(s) URL of the tenant's logo. Null falls back to the shield mark + name.</summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Brand name shown beside/instead of the shield on the hosted pages. When null/blank the pages
    /// fall back to the workspace (tenant) name. Lets a tenant show a display name without renaming
    /// the workspace or setting a logo.
    /// </summary>
    public string? BrandName { get; set; }

    /// <summary>Primary brand color as a CSS hex (e.g. <c>#5b6df5</c>). Drives buttons, links, accents.</summary>
    public string PrimaryColor { get; set; } = "#5b6df5";

    /// <summary>Text color rendered on top of <see cref="PrimaryColor"/> (button labels). Hex.</summary>
    public string ButtonTextColor { get; set; } = "#ffffff";

    /// <summary>CSS font-family stack for the hosted pages. The platform uses Inter by default.</summary>
    public string FontFamily { get; set; } = "Inter, system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif";

    /// <summary>When true the hosted pages default to dark mode.</summary>
    public bool DarkMode { get; set; }

    // --- layout -------------------------------------------------------------

    /// <summary>Where the form sits and what fills the rest of the page.</summary>
    public BrandingLayout Layout { get; set; } = BrandingLayout.CenteredPlain;

    // --- background (the branded panel, or the full page on centered-over-bg) -----

    /// <summary>What fills the branded panel / full-page background.</summary>
    public BrandingBackground Background { get; set; } = BrandingBackground.Gradient;

    /// <summary>Fill color when <see cref="Background"/> is Solid. Hex. Defaults to the primary color.</summary>
    public string BackgroundColor { get; set; } = "#5b6df5";

    /// <summary>Start color of the gradient background. Hex. Defaults to the primary color at render time when blank.</summary>
    public string GradientFrom { get; set; } = "#5b6df5";

    /// <summary>End color of the gradient background. Hex.</summary>
    public string GradientTo { get; set; } = "#1b9bc0";

    /// <summary>Absolute https(s) URL of the background image (used when <see cref="Background"/> is Image).</summary>
    public string? BackgroundImageUrl { get; set; }

    /// <summary>How the background image is sized.</summary>
    public BackgroundFit BackgroundFit { get; set; } = BackgroundFit.Cover;

    /// <summary>CSS <c>background-position</c> keyword (e.g. <c>center</c>, <c>top</c>). Sanitized on save.</summary>
    public string BackgroundPosition { get; set; } = "center";

    /// <summary>Darkening scrim over the background image, 0–100 (% black). Improves text legibility.</summary>
    public int OverlayOpacity { get; set; } = 35;

    // --- text content -------------------------------------------------------

    /// <summary>Main heading on the form (e.g. <c>Welcome back</c>).</summary>
    public string Heading { get; set; } = "Welcome back";

    /// <summary>Sub-heading beneath the form heading.</summary>
    public string Subtitle { get; set; } = "Sign in to your account.";

    /// <summary>Relative size of the form heading.</summary>
    public HeadingSize HeadingSize { get; set; } = HeadingSize.Medium;

    /// <summary>Optional one-line tagline shown beneath the brand on the panel (split layouts).</summary>
    public string? Tagline { get; set; }

    /// <summary>Optional feature bullets ("checkmark" list) shown on the branded panel.</summary>
    public List<string> FeatureBullets { get; set; } = new();

    /// <summary>Optional footer line (e.g. a copyright notice) shown beneath the form.</summary>
    public string? FooterText { get; set; }

    // --- card / shape -------------------------------------------------------

    /// <summary>Surface treatment of the form card.</summary>
    public CardStyle CardStyle { get; set; } = CardStyle.Solid;

    /// <summary>Drop-shadow strength of the form card.</summary>
    public CardShadow CardShadow { get; set; } = CardShadow.Soft;

    /// <summary>Corner radius in px applied to cards, inputs and buttons (0–16).</summary>
    public int CornerRadius { get; set; } = 8;

    // --- computed -----------------------------------------------------------

    /// <summary>
    /// <see cref="PrimaryColor"/> expressed as a CSS <c>r, g, b</c> triplet for the SAARVIX
    /// <c>--primary</c> variable. Falls back to the platform indigo if the hex can't be parsed.
    /// </summary>
    public string PrimaryColorRgb => HexToRgb(PrimaryColor) ?? "91, 109, 245";

    /// <summary>Converts a hex color to a CSS <c>r, g, b</c> triplet, or null if it can't be parsed.</summary>
    public static string? HexToRgb(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return null;
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
    public bool IsDefault
    {
        get
        {
            var d = Default;
            return LogoUrl is null
                && string.IsNullOrEmpty(BrandName)
                && PrimaryColor == d.PrimaryColor
                && ButtonTextColor == d.ButtonTextColor
                && FontFamily == d.FontFamily
                && !DarkMode
                && Layout == d.Layout
                && Background == d.Background
                && BackgroundColor == d.BackgroundColor
                && GradientFrom == d.GradientFrom
                && GradientTo == d.GradientTo
                && BackgroundImageUrl is null
                && BackgroundFit == d.BackgroundFit
                && BackgroundPosition == d.BackgroundPosition
                && OverlayOpacity == d.OverlayOpacity
                && Heading == d.Heading
                && Subtitle == d.Subtitle
                && HeadingSize == d.HeadingSize
                && string.IsNullOrEmpty(Tagline)
                && FeatureBullets.Count == 0
                && string.IsNullOrEmpty(FooterText)
                && CardStyle == d.CardStyle
                && CardShadow == d.CardShadow
                && CornerRadius == d.CornerRadius;
        }
    }
}
