using System.ComponentModel.DataAnnotations;
using Authly.Core.Enums;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Tenant-admin form for hosted-page branding and the custom auth domain.</summary>
public sealed class BrandingViewModel
{
    [Display(Name = "Logo URL")]
    [Url(ErrorMessage = "Enter an absolute http(s) URL.")]
    public string? LogoUrl { get; set; }

    [Display(Name = "Primary color")]
    [RegularExpression("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Use a hex color like #5b6df5.")]
    public string PrimaryColor { get; set; } = "#5b6df5";

    [Display(Name = "Button text color")]
    [RegularExpression("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Use a hex color like #ffffff.")]
    public string ButtonTextColor { get; set; } = "#ffffff";

    [Display(Name = "Font family")]
    public string? FontFamily { get; set; }

    public BrandingLayout Layout { get; set; } = BrandingLayout.Centered;

    [Display(Name = "Default to dark mode")]
    public bool DarkMode { get; set; }

    [Display(Name = "Tagline")]
    [StringLength(160)]
    public string? Tagline { get; set; }

    [Display(Name = "Custom domain")]
    public string? CustomDomain { get; set; }
}
