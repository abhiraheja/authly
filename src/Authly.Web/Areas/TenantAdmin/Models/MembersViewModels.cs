using System.ComponentModel.DataAnnotations;
using Authly.Core.Entities;
using Authly.Modules.Members;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Invite-an-employee form: an email plus the operator roles to grant.</summary>
public sealed class InviteMemberViewModel
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Operator roles")]
    public Guid[] RoleIds { get; set; } = Array.Empty<Guid>();

    /// <summary>The org's operator roles, for the checkbox list (populated by the controller).</summary>
    public IReadOnlyList<OperatorRole> AvailableRoles { get; set; } = Array.Empty<OperatorRole>();
}

/// <summary>Manage a single member's operator-role assignments.</summary>
public sealed class MemberRolesViewModel
{
    public Guid MembershipId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool IsOwner { get; set; }
    public IReadOnlyList<OperatorRole> AssignedRoles { get; set; } = Array.Empty<OperatorRole>();
    public IReadOnlyList<OperatorRole> AvailableRoles { get; set; } = Array.Empty<OperatorRole>();
}

/// <summary>Organization settings (rename / delete).</summary>
public sealed class OrganizationSettingsViewModel
{
    [Required, StringLength(120, MinimumLength = 2)]
    [Display(Name = "Organization name")]
    public string Name { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;
    public int ProjectCount { get; set; }
}
