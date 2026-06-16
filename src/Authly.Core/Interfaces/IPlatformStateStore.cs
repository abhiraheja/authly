namespace Authly.Core.Interfaces;

/// <summary>
/// Tiny platform-level key/value store for operational cursors and flags (e.g. the log-stream
/// position). NOT tenant-scoped — no RLS, like other control-plane state.
/// </summary>
public interface IPlatformStateStore
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, CancellationToken ct = default);
}
