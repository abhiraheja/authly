using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A tenant-authored survey shown to targeted end-users at sign-in. Shares the policies engine's
/// lifecycle, enforcement modes, targeting and scheduling; the difference is the content — a set of
/// <see cref="SurveyQuestion"/>s answered into a <see cref="SurveyResponse"/>. Tenant-scoped.
/// Maps to table "surveys".
/// </summary>
public class Survey
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Title { get; set; } = default!;
    public string? Description { get; set; }

    public PolicyStatus Status { get; set; } = PolicyStatus.Draft;
    public PolicyEnforcementMode EnforcementMode { get; set; } = PolicyEnforcementMode.Optional;

    public DateTimeOffset? SkipDeadline { get; set; }
    public DateTimeOffset? StartsAt { get; set; }

    /// <summary>After this instant the survey is closed: no longer shown; never-responded users are "Missed".</summary>
    public DateTimeOffset? CloseDate { get; set; }

    /// <summary>Targeting rules as JSON (see <see cref="Authly.Core.Policies.PolicyTargeting"/>).</summary>
    public string Targeting { get; set; } = "{}";

    // --- Settings ---
    public bool RandomizeQuestions { get; set; }
    public bool Anonymous { get; set; }
    public bool ShowProgressBar { get; set; } = true;
    public string? ThankYouMessage { get; set; }

    /// <summary>When the live revision went out. Responses before this (or before <see cref="ConsentResetAt"/>) don't count.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Admin re-requested responses — earlier ones are ignored so everyone is asked again.</summary>
    public DateTimeOffset? ConsentResetAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
