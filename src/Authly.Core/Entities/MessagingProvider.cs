using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A tenant's configured delivery provider for a channel (BYOK or managed). The
/// <see cref="Config"/> JSON holds connection details with secret fields (api keys, passwords)
/// AES-encrypted at rest. Maps to table "messaging_providers".
/// </summary>
public class MessagingProvider
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public MessageChannel Channel { get; set; }

    /// <summary>Provider key: zepto | smtp | msg91 | gupshup | log | …</summary>
    public string Provider { get; set; } = default!;

    /// <summary>managed | byok</summary>
    public string Mode { get; set; } = "byok";

    /// <summary>Serialized provider config; secret fields are encrypted. JSONB.</summary>
    public string Config { get; set; } = "{}";

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
