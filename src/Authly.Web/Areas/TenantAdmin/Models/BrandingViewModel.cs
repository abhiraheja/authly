using System.ComponentModel.DataAnnotations;
using Authly.Core.Enums;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Tenant-admin form for hosted-page branding and the custom auth domain.</summary>
public sealed class BrandingViewModel
{
    private const string HexPattern = "^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$";

    // --- identity -----------------------------------------------------------

    // No [Url] — the value may be an absolute http(s) URL or an uploaded-asset ref (/branding/asset/{id}).
    // The service validates the allowed shapes.
    [Display(Name = "Logo URL")]
    public string? LogoUrl { get; set; }

    [Display(Name = "Brand name")]
    [StringLength(60)]
    public string? BrandName { get; set; }

    [Display(Name = "Primary color")]
    [RegularExpression(HexPattern, ErrorMessage = "Use a hex color like #5b6df5.")]
    public string PrimaryColor { get; set; } = "#5b6df5";

    [Display(Name = "Button text color")]
    [RegularExpression(HexPattern, ErrorMessage = "Use a hex color like #ffffff.")]
    public string ButtonTextColor { get; set; } = "#ffffff";

    [Display(Name = "Font family")]
    public string? FontFamily { get; set; }

    [Display(Name = "Default to dark mode")]
    public bool DarkMode { get; set; }

    // --- layout -------------------------------------------------------------

    [Display(Name = "Layout")]
    public BrandingLayout Layout { get; set; } = BrandingLayout.CenteredPlain;

    // --- background ---------------------------------------------------------

    [Display(Name = "Background")]
    public BrandingBackground Background { get; set; } = BrandingBackground.Gradient;

    [Display(Name = "Background color")]
    [RegularExpression(HexPattern, ErrorMessage = "Use a hex color like #5b6df5.")]
    public string BackgroundColor { get; set; } = "#5b6df5";

    [Display(Name = "Gradient start")]
    [RegularExpression(HexPattern, ErrorMessage = "Use a hex color like #5b6df5.")]
    public string GradientFrom { get; set; } = "#5b6df5";

    [Display(Name = "Gradient end")]
    [RegularExpression(HexPattern, ErrorMessage = "Use a hex color like #1b9bc0.")]
    public string GradientTo { get; set; } = "#1b9bc0";

    // No [Url] — may be an absolute http(s) URL or an uploaded-asset ref; validated in the service.
    [Display(Name = "Background image URL")]
    public string? BackgroundImageUrl { get; set; }

    [Display(Name = "Image fit")]
    public BackgroundFit BackgroundFit { get; set; } = BackgroundFit.Cover;

    [Display(Name = "Image position")]
    public string? BackgroundPosition { get; set; } = "center";

    [Display(Name = "Overlay")]
    [Range(0, 100)]
    public int OverlayOpacity { get; set; } = 35;

    // --- text content -------------------------------------------------------

    [Display(Name = "Heading")]
    [StringLength(80)]
    public string? Heading { get; set; }

    [Display(Name = "Subtitle")]
    [StringLength(160)]
    public string? Subtitle { get; set; }

    [Display(Name = "Heading size")]
    public HeadingSize HeadingSize { get; set; } = HeadingSize.Medium;

    [Display(Name = "Tagline")]
    [StringLength(160)]
    public string? Tagline { get; set; }

    /// <summary>One bullet per line in the textarea; split/joined in the controller.</summary>
    [Display(Name = "Feature bullets")]
    public string? FeatureBulletsText { get; set; }

    [Display(Name = "Footer text")]
    [StringLength(200)]
    public string? FooterText { get; set; }

    // --- card / shape -------------------------------------------------------

    [Display(Name = "Card style")]
    public CardStyle CardStyle { get; set; } = CardStyle.Solid;

    [Display(Name = "Card shadow")]
    public CardShadow CardShadow { get; set; } = CardShadow.Soft;

    [Display(Name = "Corner radius")]
    [Range(0, 16)]
    public int CornerRadius { get; set; } = 8;

    // --- domain -------------------------------------------------------------

    [Display(Name = "Custom domain")]
    public string? CustomDomain { get; set; }
}
