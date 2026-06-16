using System.ComponentModel.DataAnnotations;
using Authly.Modules.Abac;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Create/edit form for an ABAC access policy.</summary>
public sealed class AccessPolicyViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(120), Display(Name = "Name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500), Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Effect")]
    public PolicyEffect Effect { get; set; } = PolicyEffect.Allow;

    [Required, Display(Name = "Action (exact, prefix*, or *)")]
    public string Action { get; set; } = "*";

    [Required, Display(Name = "Resource type (exact, prefix*, or *)")]
    public string ResourceType { get; set; } = "*";

    [Display(Name = "Conditions (JSON array)")]
    public string? ConditionsJson { get; set; } = "[]";

    [Display(Name = "Priority")]
    public int Priority { get; set; }

    [Display(Name = "Enabled")]
    public bool Enabled { get; set; } = true;
}

/// <summary>The "test this policy set" console on the policies page.</summary>
public sealed class AccessPolicyTestViewModel
{
    [Display(Name = "Action")] public string? Action { get; set; }
    [Display(Name = "Resource type")] public string? ResourceType { get; set; }
    [Display(Name = "Subject attributes (JSON object)")] public string? SubjectJson { get; set; } = "{}";
    [Display(Name = "Resource attributes (JSON object)")] public string? ResourceJson { get; set; } = "{}";
    [Display(Name = "Environment attributes (JSON object)")] public string? EnvironmentJson { get; set; } = "{}";
}
