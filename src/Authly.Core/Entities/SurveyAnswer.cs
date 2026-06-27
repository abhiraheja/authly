namespace Authly.Core.Entities;

/// <summary>
/// One answer within a <see cref="SurveyResponse"/>. The populated field depends on the question
/// type: free text → <see cref="TextValue"/>; number/rating → <see cref="NumberValue"/>; choice(s) →
/// <see cref="OptionIds"/> (JSON array of option ids). Maps to table "survey_answers".
/// </summary>
public class SurveyAnswer
{
    public Guid Id { get; set; }
    public Guid ResponseId { get; set; }
    public Guid QuestionId { get; set; }
    public Guid TenantId { get; set; }

    public string? TextValue { get; set; }
    public double? NumberValue { get; set; }

    /// <summary>Selected option ids as a JSON array (single-element for single choice). Empty otherwise.</summary>
    public string? OptionIds { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
