using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A tenant-authored policy (Terms &amp; Conditions, Privacy Policy, or any norm) that targeted
/// end-users are asked to accept/reject/skip at sign-in. The content lives in immutable
/// <see cref="PolicyVersion"/>s (so re-consent can be required when a new version is published);
/// the <see cref="CurrentVersionId"/> points at the live one. Tenant-scoped. Maps to table "policies".
/// </summary>
public class Policy
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant (project) — policies are tenant-scoped.</summary>
    public Guid TenantId { get; set; }

    public string Title { get; set; } = default!;

    /// <summary>Short admin-facing summary of what this policy is for. Optional.</summary>
    public string? Description { get; set; }

    public PolicyStatus Status { get; set; } = PolicyStatus.Draft;

    public PolicyEnforcementMode EnforcementMode { get; set; } = PolicyEnforcementMode.Mandatory;

    /// <summary>For <see cref="PolicyEnforcementMode.SkippableUntil"/>: skip is allowed until this instant; after it, accept is required.</summary>
    public DateTimeOffset? SkipDeadline { get; set; }

    /// <summary>The policy is not shown before this instant. Null = show as soon as published.</summary>
    public DateTimeOffset? StartsAt { get; set; }

    /// <summary>After this instant the policy is no longer shown or enforced. Null = no expiry (legal default).</summary>
    public DateTimeOffset? CloseDate { get; set; }

    /// <summary>Targeting rules as JSON (see <see cref="Authly.Core.Policies.PolicyTargeting"/>). Defaults to "all users".</summary>
    public string Targeting { get; set; } = "{}";

    // --- Editable draft content. Snapshotted into an immutable PolicyVersion on publish. ---

    public PolicyContentType DraftContentType { get; set; } = PolicyContentType.Html;

    /// <summary>Sanitized draft HTML body (when <see cref="DraftContentType"/> is Html).</summary>
    public string? DraftHtmlContent { get; set; }

    /// <summary>Draft uploaded PDF asset id (when <see cref="DraftContentType"/> is Pdf).</summary>
    public Guid? DraftAssetId { get; set; }

    /// <summary>The live version users are asked to consent to. Null until first published.</summary>
    public Guid? CurrentVersionId { get; set; }

    /// <summary>When set, decisions made before this instant are ignored — the admin re-requested
    /// acceptance, so everyone is prompted again (without erasing the prior audit trail).</summary>
    public DateTimeOffset? ConsentResetAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
}
