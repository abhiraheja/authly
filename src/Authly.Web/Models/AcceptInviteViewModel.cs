using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Models;

/// <summary>Public invite-accept form: the invitee confirms by setting a password for their console account.</summary>
public sealed class AcceptInviteViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

    /// <summary>Display-only — the address the invite was sent to.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Set only for a brand-new account; existing accounts keep their current password.</summary>
    public bool RequiresPassword { get; set; } = true;

    [DataType(DataType.Password), StringLength(128, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password), Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
