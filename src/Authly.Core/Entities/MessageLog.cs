using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// An immutable record of one delivery attempt. Holds routing/outcome metadata only — never the
/// rendered body or template variables (which may contain OTPs/links). Maps to table "message_log".
/// </summary>
public class MessageLog
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public MessageChannel Channel { get; set; }

    /// <summary>The email address or phone number the message was sent to.</summary>
    public string Recipient { get; set; } = default!;

    public string? TemplateKey { get; set; }

    /// <summary>queued | sent | failed</summary>
    public string Status { get; set; } = "queued";

    public string? Error { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
