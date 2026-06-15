using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Areas.SuperAdmin.Models;

/// <summary>Create/edit form for a platform announcement.</summary>
public sealed class AnnouncementViewModel
{
    public Guid? Id { get; set; }

    [Required, StringLength(160), Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(2000), Display(Name = "Message")]
    public string Body { get; set; } = string.Empty;

    [Required, Display(Name = "Severity")]
    public string Severity { get; set; } = "info";

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Expires at (UTC, optional)")]
    public DateTimeOffset? ExpiresAt { get; set; }

    public static readonly string[] Severities = { "info", "warning", "danger", "success" };
}
