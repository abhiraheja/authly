using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// An (append-only) record of a user's decision on a specific <see cref="PolicyVersion"/> — the
/// consent audit trail. <see cref="SessionId"/> lets a "skip" apply only to the current session
/// (so a skippable policy is re-asked on the next sign-in). Tenant-scoped. Maps to table
/// "policy_decisions".
/// </summary>
public class PolicyDecision
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid PolicyId { get; set; }
    public Guid PolicyVersionId { get; set; }

    /// <summary>Denormalized version number for easy display in the portal history.</summary>
    public int Version { get; set; }

    public PolicyDecisionType Decision { get; set; }

    /// <summary>The session the decision was made in — a "skip" only satisfies its own session.</summary>
    public Guid? SessionId { get; set; }

    /// <summary>The OAuth application the user was authenticating for, when known (app-targeted policies).</summary>
    public Guid? ApplicationId { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    public DateTimeOffset DecidedAt { get; set; }
}
