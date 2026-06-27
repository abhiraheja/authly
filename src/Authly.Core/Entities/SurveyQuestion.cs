using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>A question within a <see cref="Survey"/>. Maps to table "survey_questions".</summary>
public class SurveyQuestion
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Display order within the survey (ascending).</summary>
    public int Order { get; set; }

    public SurveyQuestionType Type { get; set; }

    public string Title { get; set; } = default!;
    public string? HelpText { get; set; }
    public bool Required { get; set; }

    /// <summary>Optional image/video URL shown with the question.</summary>
    public string? MediaUrl { get; set; }

    // --- Type-specific settings (only the relevant ones are used per type) ---
    public int? ScaleMin { get; set; }
    public int? ScaleMax { get; set; }
    public bool RandomizeOptions { get; set; }
    public string? Placeholder { get; set; }
}
