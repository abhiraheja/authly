using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A user's response to a survey (one per attempt). <see cref="SessionId"/> lets a "skip" apply only
/// to the current session. <see cref="UserId"/> is null for anonymous surveys. Tenant-scoped.
/// Maps to table "survey_responses".
/// </summary>
public class SurveyResponse
{
    public Guid Id { get; set; }
    public Guid SurveyId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Null when the survey is anonymous.</summary>
    public Guid? UserId { get; set; }

    public Guid? SessionId { get; set; }

    public SurveyResponseStatus Status { get; set; }

    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}
