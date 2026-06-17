using Authly.Core.Branding;
using Authly.Core.Enums;

namespace Authly.Modules.Branding;

/// <summary>Form input for editing a tenant's hosted-page branding.</summary>
public sealed class BrandingInput
{
    // identity
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#5b6df5";
    public string ButtonTextColor { get; set; } = "#ffffff";
    public string? FontFamily { get; set; }
    public bool DarkMode { get; set; }

    // layout
    public BrandingLayout Layout { get; set; } = BrandingLayout.CenteredPlain;

    // background
    public BrandingBackground Background { get; set; } = BrandingBackground.Gradient;
    public string GradientFrom { get; set; } = "#5b6df5";
    public string GradientTo { get; set; } = "#1b9bc0";
    public string? BackgroundImageUrl { get; set; }
    public BackgroundFit BackgroundFit { get; set; } = BackgroundFit.Cover;
    public string? BackgroundPosition { get; set; }
    public int OverlayOpacity { get; set; } = 35;

    // text
    public string? Heading { get; set; }
    public string? Subtitle { get; set; }
    public HeadingSize HeadingSize { get; set; } = HeadingSize.Medium;
    public string? Tagline { get; set; }
    public List<string> FeatureBullets { get; set; } = new();
    public string? FooterText { get; set; }

    // card / shape
    public CardStyle CardStyle { get; set; } = CardStyle.Solid;
    public CardShadow CardShadow { get; set; } = CardShadow.Soft;
    public int CornerRadius { get; set; } = 8;

    /// <summary>Copies an existing branding into an input, so callers can override just a field or two.</summary>
    public static BrandingInput From(TenantBranding b) => new()
    {
        LogoUrl = b.LogoUrl,
        PrimaryColor = b.PrimaryColor,
        ButtonTextColor = b.ButtonTextColor,
        FontFamily = b.FontFamily,
        DarkMode = b.DarkMode,
        Layout = b.Layout,
        Background = b.Background,
        GradientFrom = b.GradientFrom,
        GradientTo = b.GradientTo,
        BackgroundImageUrl = b.BackgroundImageUrl,
        BackgroundFit = b.BackgroundFit,
        BackgroundPosition = b.BackgroundPosition,
        OverlayOpacity = b.OverlayOpacity,
        Heading = b.Heading,
        Subtitle = b.Subtitle,
        HeadingSize = b.HeadingSize,
        Tagline = b.Tagline,
        FeatureBullets = new List<string>(b.FeatureBullets),
        FooterText = b.FooterText,
        CardStyle = b.CardStyle,
        CardShadow = b.CardShadow,
        CornerRadius = b.CornerRadius
    };
}

/// <summary>Thrown when submitted branding or a custom domain fails validation.</summary>
public sealed class BrandingConfigInvalidException : Exception
{
    public BrandingConfigInvalidException(string message) : base(message) { }
}
