using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Policies;

namespace Authly.Modules.Surveys;

/// <summary>Admin input for survey meta + enforcement + targeting + settings.</summary>
public sealed class SurveyEditInput
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public PolicyEnforcementMode EnforcementMode { get; set; } = PolicyEnforcementMode.Optional;
    public DateTimeOffset? SkipDeadline { get; set; }
    public DateTimeOffset? StartsAt { get; set; }
    public DateTimeOffset? CloseDate { get; set; }
    public PolicyTargeting Targeting { get; set; } = PolicyTargeting.All();
    public bool RandomizeQuestions { get; set; }
    public bool Anonymous { get; set; }
    public bool ShowProgressBar { get; set; } = true;
    public string? ThankYouMessage { get; set; }
}

/// <summary>Admin input for a question (with its options for choice types).</summary>
public sealed class QuestionInput
{
    public SurveyQuestionType Type { get; set; }
    public string Title { get; set; } = "";
    public string? HelpText { get; set; }
    public bool Required { get; set; }
    public string? MediaUrl { get; set; }
    public int? ScaleMin { get; set; }
    public int? ScaleMax { get; set; }
    public bool RandomizeOptions { get; set; }
    public string? Placeholder { get; set; }
    /// <summary>Option labels for choice types (one per line in the UI).</summary>
    public List<string> Options { get; set; } = new();
}

/// <summary>A question plus its options, for the builder and runner.</summary>
public sealed record QuestionWithOptions(SurveyQuestion Question, IReadOnlyList<SurveyQuestionOption> Options);

/// <summary>A survey pending for a user at sign-in.</summary>
public sealed record PendingSurvey(Guid SurveyId, string Title, string? Description,
    PolicyEnforcementMode Mode, bool HardBlock, bool AllowSkip, bool AllowReject);

/// <summary>A survey prepared for display in the runner (questions ordered/randomized with options).</summary>
public sealed record SurveyForRunner(Survey Survey, IReadOnlyList<QuestionWithOptions> Questions);

/// <summary>A single submitted answer from the runner.</summary>
public sealed class SurveyAnswerInput
{
    public Guid QuestionId { get; set; }
    public string? Text { get; set; }
    public double? Number { get; set; }
    public List<Guid> OptionIds { get; set; } = new();
}

/// <summary>Thrown when a survey can't be published or a submission is invalid.</summary>
public sealed class SurveyInvalidException(string message) : Exception(message);

// --- Reporting ---

public sealed class SurveyReport
{
    public required Survey Survey { get; init; }
    public int Completed { get; init; }
    public int InProgress { get; init; }
    public int Skipped { get; init; }
    public int Declined { get; init; }
    public required IReadOnlyList<QuestionReport> Questions { get; init; }
}

public sealed class QuestionReport
{
    public required SurveyQuestion Question { get; init; }
    public int AnswerCount { get; init; }
    /// <summary>For choice/yes-no: label → count.</summary>
    public IReadOnlyList<(string Label, int Count)> OptionCounts { get; init; } = Array.Empty<(string, int)>();
    /// <summary>For number/rating: average of numeric answers (null if none).</summary>
    public double? Average { get; init; }
    /// <summary>For text: a sample of recent free-text answers.</summary>
    public IReadOnlyList<string> TextSamples { get; init; } = Array.Empty<string>();
}
