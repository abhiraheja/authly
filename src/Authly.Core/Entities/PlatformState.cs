namespace Authly.Core.Entities;

/// <summary>A platform-level key/value row (control-plane state, not tenant-scoped).</summary>
public class PlatformState
{
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
    public DateTimeOffset UpdatedAt { get; set; }
}
