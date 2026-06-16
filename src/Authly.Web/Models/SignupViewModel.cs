using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Models;

/// <summary>Public self-service signup form: create a workspace and its first administrator.</summary>
public sealed class SignupViewModel
{
    [Required, Display(Name = "Company / workspace name"), StringLength(120, MinimumLength = 2)]
    public string CompanyName { get; set; } = string.Empty;

    [Required, EmailAddress, Display(Name = "Work email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "First name"), StringLength(80)]
    public string? FirstName { get; set; }

    [Display(Name = "Last name"), StringLength(80)]
    public string? LastName { get; set; }

    [Required, DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
