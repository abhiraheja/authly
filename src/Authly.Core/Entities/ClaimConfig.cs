using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A tenant-defined custom token claim (§4.13). Resolved during claim assembly (§5.6 step 2–3):
/// <see cref="ClaimSourceType.Static"/> emits <see cref="Source"/> verbatim,
/// <see cref="ClaimSourceType.Metadata"/> reads a dotted path from the user's metadata, and
/// <see cref="ClaimSourceType.Webhook"/> POSTs to <see cref="Source"/> for the value.
/// Maps to table "claim_configs".
/// </summary>
public class ClaimConfig
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>Scope to a single application, or null for tenant-wide.</summary>
    public Guid? ApplicationId { get; set; }

    public ClaimTokenType TokenType { get; set; }
    public ClaimSourceType Type { get; set; }

    /// <summary>The claim name written into the token.</summary>
    public string ClaimName { get; set; } = default!;

    /// <summary>Static value, metadata path, or webhook URL — interpreted per <see cref="Type"/>.</summary>
    public string? Source { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
