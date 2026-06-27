using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// An immutable snapshot of a <see cref="Policy"/>'s content at publish time. Each publish creates a
/// new version (incrementing <see cref="Version"/>); user consent is tied to a specific version, so
/// publishing a new one re-prompts everyone who only accepted the old. Maps to table "policy_versions".
/// </summary>
public class PolicyVersion
{
    public Guid Id { get; set; }
    public Guid PolicyId { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Monotonic version number for this policy, starting at 1.</summary>
    public int Version { get; set; }

    public PolicyContentType ContentType { get; set; } = PolicyContentType.Html;

    /// <summary>Sanitized HTML body when <see cref="ContentType"/> is Html.</summary>
    public string? HtmlContent { get; set; }

    /// <summary>The uploaded PDF asset id when <see cref="ContentType"/> is Pdf.</summary>
    public Guid? AssetId { get; set; }

    /// <summary>Optional admin note describing what changed in this version.</summary>
    public string? Notes { get; set; }

    public DateTimeOffset PublishedAt { get; set; }
}
