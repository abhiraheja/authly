using Authly.Core.Enums;

namespace Authly.Modules.Branding;

/// <summary>Form input for editing a tenant's hosted-page branding.</summary>
public sealed class BrandingInput
{
    public string? LogoUrl { get; set; }
    public string PrimaryColor { get; set; } = "#5b6df5";
    public string ButtonTextColor { get; set; } = "#ffffff";
    public string? FontFamily { get; set; }
    public BrandingLayout Layout { get; set; } = BrandingLayout.Centered;
    public bool DarkMode { get; set; }
    public string? Tagline { get; set; }
}

/// <summary>Thrown when submitted branding or a custom domain fails validation.</summary>
public sealed class BrandingConfigInvalidException : Exception
{
    public BrandingConfigInvalidException(string message) : base(message) { }
}
