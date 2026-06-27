namespace Authly.Core.Enums;

/// <summary>
/// The kind of a survey question. Drives how it renders and how the answer is stored
/// (<c>TextValue</c> / <c>NumberValue</c> / <c>OptionIds</c>). Extensible — add a case here plus a
/// renderer to introduce a new type (matrix, ranking, file upload, etc. are future work).
/// </summary>
public enum SurveyQuestionType
{
    SingleChoice,
    MultipleChoice,
    Dropdown,
    ShortText,
    LongText,
    Number,
    Rating,
    YesNo,
    Date
}

/// <summary>Lifecycle/state of a user's response to a survey.</summary>
public enum SurveyResponseStatus
{
    /// <summary>Started but not submitted (partial save).</summary>
    InProgress,
    /// <summary>Submitted.</summary>
    Completed,
    /// <summary>Deferred for the current session (skippable surveys).</summary>
    Skipped,
    /// <summary>Explicitly declined (optional surveys).</summary>
    Declined,
    /// <summary>Never answered before the survey closed.</summary>
    Missed
}
