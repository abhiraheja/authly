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

/// <summary>Create-role form.</summary>
public sealed class CreateRoleViewModel
{
    [Required, Display(Name = "Role name")]
    [RegularExpression("^[a-z0-9_]+$", ErrorMessage = "Use lower-case letters, digits and underscores.")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Description")]
    public string? Description { get; set; }
}

/// <summary>Create-API-key form.</summary>
public sealed class CreateApiKeyViewModel
{
    [Required, Display(Name = "Key name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Scopes (space-separated, blank = full access)")]
    public string? Scopes { get; set; }
}

/// <summary>Manage a single user's role assignments.</summary>
public sealed class UserRolesViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<Authly.Core.Entities.Role> AssignedRoles { get; set; } = Array.Empty<Authly.Core.Entities.Role>();
    public IReadOnlyList<Authly.Core.Entities.Role> AvailableRoles { get; set; } = Array.Empty<Authly.Core.Entities.Role>();
}
