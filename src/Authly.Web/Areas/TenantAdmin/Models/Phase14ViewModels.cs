using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Minimal branding step in the onboarding wizard (full controls live on the Branding page).</summary>
public sealed class OnboardingBrandingViewModel
{
    [Display(Name = "Logo URL")]
    [Url(ErrorMessage = "Enter a valid URL.")]
    public string? LogoUrl { get; set; }

    [Required, Display(Name = "Primary color")]
    [RegularExpression("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$", ErrorMessage = "Use a hex color like #5b6df5.")]
    public string PrimaryColor { get; set; } = "#5b6df5";
}

/// <summary>Sandbox test-login form — validates credentials against this tenant's user store.</summary>
public sealed class SandboxLoginViewModel
{
    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}
