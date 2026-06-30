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

    [Display(Name = "Post-logout redirect URIs (one per line)")]
    public string? PostLogoutRedirectUris { get; set; }

    [Display(Name = "Scopes (space-separated)")]
    public string Scopes { get; set; } = "openid profile email";

    [Display(Name = "Allow self-service sign-up")]
    public bool AllowSignup { get; set; } = true;
}

/// <summary>Edit-application form. Type and credentials are fixed; only these fields are editable.</summary>
public sealed class EditApplicationViewModel
{
    [Required, Display(Name = "Application name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Redirect URIs (one per line)")]
    public string? RedirectUris { get; set; }

    [Display(Name = "Post-logout redirect URIs (one per line)")]
    public string? PostLogoutRedirectUris { get; set; }

    [Display(Name = "Scopes (space-separated)")]
    public string Scopes { get; set; } = string.Empty;

    [Display(Name = "Allow self-service sign-up")]
    public bool AllowSignup { get; set; } = true;

    /// <summary>The application's (immutable) type — display-only, drives whether the sign-up toggle is shown
    /// (Machine clients have no sign-up). Round-trips via a hidden field so a re-rendered form keeps it.</summary>
    public ApplicationType Type { get; set; }
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

/// <summary>One row in the tenant users list: the user plus the sign-in sources linked to them
/// ("password" for a local credential, plus any social provider keys like "google").</summary>
public sealed record UserListRow(Authly.Core.Entities.User User, IReadOnlyList<string> Providers);

/// <summary>Manage a single user's role assignments.</summary>
public sealed class UserRolesViewModel
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<Authly.Core.Entities.Role> AssignedRoles { get; set; } = Array.Empty<Authly.Core.Entities.Role>();
    public IReadOnlyList<Authly.Core.Entities.Role> AvailableRoles { get; set; } = Array.Empty<Authly.Core.Entities.Role>();
}

/// <summary>Full tenant-admin view of one user: profile, security, sessions, roles and raw data.</summary>
public sealed class UserDetailViewModel
{
    public Authly.Core.Entities.User User { get; set; } = default!;

    public IReadOnlyList<Authly.Core.Entities.Role> AssignedRoles { get; set; } = Array.Empty<Authly.Core.Entities.Role>();
    public IReadOnlyList<Authly.Core.Entities.Role> AvailableRoles { get; set; } = Array.Empty<Authly.Core.Entities.Role>();

    public IReadOnlyList<Authly.Core.Entities.MfaFactor> Factors { get; set; } = Array.Empty<Authly.Core.Entities.MfaFactor>();
    public int UnusedBackupCodes { get; set; }
    public IReadOnlyList<Authly.Core.Entities.Session> Sessions { get; set; } = Array.Empty<Authly.Core.Entities.Session>();

    /// <summary>Pretty-printed user-editable metadata JSON.</summary>
    public string UserMetadataJson { get; set; } = "{}";
    /// <summary>Pretty-printed backend-only metadata JSON.</summary>
    public string AppMetadataJson { get; set; } = "{}";
    /// <summary>Pretty-printed full account record (password hash redacted).</summary>
    public string RawJson { get; set; } = "{}";
}

/// <summary>Tenant-admin edit of a user's profile fields.</summary>
public sealed class EditUserProfileViewModel
{
    [Display(Name = "First name")]
    public string? FirstName { get; set; }

    [Display(Name = "Last name")]
    public string? LastName { get; set; }

    [Display(Name = "Phone")]
    public string? Phone { get; set; }

    [Display(Name = "Timezone")]
    public string? Timezone { get; set; }

    [Display(Name = "Locale")]
    public string? Locale { get; set; }
}
