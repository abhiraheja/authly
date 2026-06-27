namespace Authly.Core.Entities;

/// <summary>A choice for a choice-type <see cref="SurveyQuestion"/>. Maps to table "survey_question_options".</summary>
public class SurveyQuestionOption
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public Guid SurveyId { get; set; }
    public Guid TenantId { get; set; }

    public int Order { get; set; }
    public string Label { get; set; } = default!;

    /// <summary>Optional stored value (defaults to the label when null).</summary>
    public string? Value { get; set; }

    /// <summary>Optional image for image-choice questions.</summary>
    public string? ImageUrl { get; set; }
}
