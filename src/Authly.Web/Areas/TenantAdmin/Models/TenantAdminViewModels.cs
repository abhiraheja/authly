using System.ComponentModel.DataAnnotations;
using Authly.Core.Enums;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Tenant-admin sign-in form.</summary>
public sealed class TenantAdminLoginViewModel
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }
}

/// <summary>Create-application form.</summary>
public sealed class CreateApplicationViewModel
{
    [Required, Display(Name = "Application name")]
    public string Name { get; set; } = string.Empty;

    [Required, Display(Name = "Application type")]
    public ApplicationType Type { get; set; } = ApplicationType.Web;

    [Display(Name = "Redirect URIs (one per line)")]
    public string? RedirectUris { get; set; }

    [Display(Name = "Scopes (space-separated)")]
    public string Scopes { get; set; } = "openid profile email";
}
