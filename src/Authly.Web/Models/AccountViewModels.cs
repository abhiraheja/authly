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

    /// <summary>Optional mobile number (E.164). When phone sign-up is enabled and a number is given,
    /// it is verified via WhatsApp OTP after registration so it can be used to sign in.</summary>
    [Phone, Display(Name = "Mobile number")]
    public string? Phone { get; set; }

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

/// <summary>End-user phone sign-in form: number plus either a password or a one-time code.</summary>
public sealed class PhoneLoginViewModel
{
    [Required, Phone, Display(Name = "Mobile number")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Optional — only used when the user chose the password sign-in mode.</summary>
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    /// <summary>"otp" (default, WhatsApp code) or "password".</summary>
    public string Mode { get; set; } = "otp";

    public string? ReturnUrl { get; set; }
}

/// <summary>Entering the WhatsApp OTP for a phone sign-in or a post-signup phone verification.</summary>
public sealed class PhoneOtpViewModel
{
    [Required, Display(Name = "Verification code")]
    public string Code { get; set; } = string.Empty;

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
