using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Areas.SuperAdmin.Models;

public sealed class CreateTenantViewModel
{
    [Required]
    [Display(Name = "Organization name")]
    public string Name { get; set; } = "";

    [Display(Name = "Slug (optional)")]
    [RegularExpression("^[a-z0-9-]*$", ErrorMessage = "Use lowercase letters, numbers, and hyphens only.")]
    public string? Slug { get; set; }
}
