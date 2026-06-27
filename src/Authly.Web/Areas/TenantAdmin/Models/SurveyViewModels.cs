using System.ComponentModel.DataAnnotations;
using Authly.Core.Entities;
using Authly.Modules.Surveys;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Create/edit form for a survey (meta + enforcement + targeting + settings).</summary>
public sealed class SurveyEditViewModel
{
    public Guid? Id { get; set; }

    [Required, Display(Name = "Title")]
    public string Title { get; set; } = "";
    public string? Description { get; set; }

    public string EnforcementMode { get; set; } = "Optional";

    [DataType(DataType.DateTime)] public DateTime? SkipDeadline { get; set; }
    [DataType(DataType.DateTime)] public DateTime? StartsAt { get; set; }
    [DataType(DataType.DateTime)] public DateTime? CloseDate { get; set; }

    public string Audience { get; set; } = "all";
    public List<Guid> ApplicationIds { get; set; } = new();
    public List<string> AuthMethods { get; set; } = new();
    public List<string> Providers { get; set; } = new();

    public bool RandomizeQuestions { get; set; }
    public bool Anonymous { get; set; }
    public bool ShowProgressBar { get; set; } = true;
    public string? ThankYouMessage { get; set; }

    // Read-only context
    public string Status { get; set; } = "Draft";
    public List<AppOption> AvailableApps { get; set; } = new();
    public List<string> AvailableProviders { get; set; } = new();
    public IReadOnlyList<QuestionWithOptions> Questions { get; set; } = Array.Empty<QuestionWithOptions>();

    public sealed record AppOption(Guid Id, string Name);
}

/// <summary>Add-a-question form on the survey builder.</summary>
public sealed class QuestionFormViewModel
{
    public string Type { get; set; } = "SingleChoice";
    [Required] public string Title { get; set; } = "";
    public string? HelpText { get; set; }
    public bool Required { get; set; }
    public string? MediaUrl { get; set; }
    public int? ScaleMin { get; set; }
    public int? ScaleMax { get; set; }
    public bool RandomizeOptions { get; set; }
    public string? Placeholder { get; set; }
    /// <summary>One option per line (for choice types).</summary>
    public string? Options { get; set; }
}

/// <summary>Survey report wrapper for the responses view.</summary>
public sealed class SurveyReportViewModel
{
    public required SurveyReport Report { get; init; }
}
