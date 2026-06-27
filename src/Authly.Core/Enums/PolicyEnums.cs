namespace Authly.Core.Enums;

/// <summary>Lifecycle of a <see cref="Authly.Core.Entities.Policy"/>.</summary>
public enum PolicyStatus
{
    /// <summary>Being authored — never shown to end-users.</summary>
    Draft,
    /// <summary>Live — evaluated at sign-in and shown to targeted users.</summary>
    Published,
    /// <summary>Retired — no longer shown or enforced (kept for the audit trail).</summary>
    Archived
}

/// <summary>How strongly a published policy is enforced at sign-in.</summary>
public enum PolicyEnforcementMode
{
    /// <summary>Must be accepted to proceed — no skip, no reject. Blocks until accepted.</summary>
    Mandatory,
    /// <summary>Skippable until <see cref="Authly.Core.Entities.Policy.SkipDeadline"/> (asked every sign-in);
    /// after the deadline it becomes blocking (accept required).</summary>
    SkippableUntil,
    /// <summary>Never blocks — accept/reject/skip are all allowed; used to gather an opinion once.</summary>
    Optional
}

/// <summary>How a policy version's content is authored/stored.</summary>
public enum PolicyContentType
{
    /// <summary>Inline sanitized HTML.</summary>
    Html,
    /// <summary>An uploaded PDF document (bytes in <see cref="Authly.Core.Entities.PolicyAsset"/>).</summary>
    Pdf
}

/// <summary>A user's decision on a specific policy version.</summary>
public enum PolicyDecisionType
{
    Accepted,
    Rejected,
    Skipped
}
