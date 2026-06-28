using System.ComponentModel.DataAnnotations;
using Authly.Core.Entities;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Row in the policies list.</summary>
public sealed class PolicyListItem
{
    public required Policy Policy { get; init; }
    public int PendingOrLiveVersion { get; init; }
}

/// <summary>Create/edit form for a policy (meta + draft content + enforcement + targeting).</summary>
public sealed class PolicyEditViewModel
{
    public Guid? Id { get; set; }

    [Required, Display(Name = "Title")]
    public string Title { get; set; } = "";

    [Display(Name = "Description")]
    public string? Description { get; set; }

    /// <summary>Mandatory | SkippableUntil | Optional.</summary>
    public string EnforcementMode { get; set; } = "Mandatory";

    [DataType(DataType.DateTime)]
    public DateTime? SkipDeadline { get; set; }
    [DataType(DataType.DateTime)]
    public DateTime? StartsAt { get; set; }
    [DataType(DataType.DateTime)]
    public DateTime? CloseDate { get; set; }

    /// <summary>Html | Pdf.</summary>
    public string ContentType { get; set; } = "Html";
    public string? HtmlContent { get; set; }

    /// <summary>all | applications | authMethods | providers.</summary>
    public string Audience { get; set; } = "all";
    public List<Guid> ApplicationIds { get; set; } = new();
    public List<string> AuthMethods { get; set; } = new();
    public List<string> Providers { get; set; } = new();
    public List<string> Roles { get; set; } = new();
    public string Match { get; set; } = "any";

    // --- Read-only context for the view ---
    public string Status { get; set; } = "Draft";
    public bool HasDraftPdf { get; set; }
    public string? DraftPdfAssetId { get; set; }
    public int? CurrentVersion { get; set; }
    public List<AppOption> AvailableApps { get; set; } = new();
    public List<string> AvailableProviders { get; set; } = new();
    public List<string> AvailableRoles { get; set; } = new();

    public sealed record AppOption(Guid Id, string Name);

    public TargetingEditorModel ToTargetingEditor(string previewUrl) => new()
    {
        Audience = Audience, ApplicationIds = ApplicationIds, AuthMethods = AuthMethods,
        Providers = Providers, Roles = Roles, Match = Match,
        AvailableApps = AvailableApps.Select(a => new TargetingEditorModel.AppOption(a.Id, a.Name)).ToList(),
        AvailableProviders = AvailableProviders, AvailableRoles = AvailableRoles, PreviewUrl = previewUrl
    };
}

/// <summary>Responses/report for a published policy.</summary>
public sealed class PolicyResponsesViewModel
{
    public required Policy Policy { get; init; }
    public int Accepted { get; init; }
    public int Rejected { get; init; }
    public int Skipped { get; init; }
    public required IReadOnlyList<PolicyDecision> Recent { get; init; }
    /// <summary>userId → display label (email), for showing who decided.</summary>
    public IReadOnlyDictionary<Guid, string> UserLabels { get; init; } = new Dictionary<Guid, string>();
}
