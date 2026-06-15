using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Areas.Portal.Models;

/// <summary>Profile view + edit form for the portal.</summary>
public sealed class PortalProfileViewModel
{
    public string Email { get; set; } = "";
    public bool EmailVerified { get; set; }

    [Display(Name = "First name"), StringLength(100)]
    public string? FirstName { get; set; }

    [Display(Name = "Last name"), StringLength(100)]
    public string? LastName { get; set; }

    [Display(Name = "Time zone"), StringLength(64)]
    public string Timezone { get; set; } = "UTC";

    [Display(Name = "Locale"), StringLength(16)]
    public string Locale { get; set; } = "en";
}

/// <summary>Authenticated password change in the portal.</summary>
public sealed class PortalChangePasswordViewModel
{
    /// <summary>False for social-only accounts setting a password for the first time (no current to verify).</summary>
    public bool HasPassword { get; set; } = true;

    [DataType(DataType.Password), Display(Name = "Current password")]
    public string? CurrentPassword { get; set; }

    [Required, DataType(DataType.Password), Display(Name = "New password")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    public string NewPassword { get; set; } = "";

    [Required, DataType(DataType.Password), Display(Name = "Confirm new password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = "";
}
