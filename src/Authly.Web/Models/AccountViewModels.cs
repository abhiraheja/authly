using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Models;

/// <summary>Self-service registration form for a tenant end-user.</summary>
public sealed class RegisterViewModel
{
    [Required, EmailAddress, Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "First name")]
    public string? FirstName { get; set; }

    [Display(Name = "Last name")]
    public string? LastName { get; set; }

    [Required, DataType(DataType.Password)]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    public string Password { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>Consent to the terms of service and privacy policy (GDPR/DPDP) — required to register.</summary>
    [Display(Name = "I agree to the Terms of Service and Privacy Policy")]
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Terms and Privacy Policy to create an account.")]
    public bool AcceptTerms { get; set; }

    /// <summary>Where to continue after signing in (e.g. the OAuth /connect/authorize request). Carried so signup keeps the original flow.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>End-user email/password sign-in form.</summary>
public sealed class UserLoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>Request a password-reset link.</summary>
public sealed class ForgotPasswordViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Original post-login destination, carried so the user returns to it after signing in.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>Request a passwordless magic sign-in link, or initiate account recovery.</summary>
public sealed class EmailOnlyViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>Original post-login destination, carried so the user returns to it after signing in.</summary>
    public string? ReturnUrl { get; set; }
}

/// <summary>Choose a new password using a reset token.</summary>
public sealed class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "New password")]
    [StringLength(128, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
