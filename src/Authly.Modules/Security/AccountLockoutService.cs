using Authly.Core.Security;

namespace Authly.Modules.Security;

/// <summary>Account lockout with exponential backoff, backed by the Redis attempt store.</summary>
public interface IAccountLockoutService
{
    /// <summary>True if the identity is currently locked out.</summary>
    Task<bool> IsLockedAsync(Guid tenantId, string email, CancellationToken ct = default);

    /// <summary>Records a failed login; locks the account once the threshold is reached.</summary>
    Task RecordFailureAsync(Guid tenantId, string email, CancellationToken ct = default);

    /// <summary>Clears failures + lockout (after a successful login or a password reset = self-service unlock).</summary>
    Task ResetAsync(Guid tenantId, string email, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AccountLockoutService : IAccountLockoutService
{
    private static readonly TimeSpan FailureRetention = TimeSpan.FromHours(1);

    private readonly ILoginAttemptStore _store;
    private readonly ISecuritySettingsService _settings;

    public AccountLockoutService(ILoginAttemptStore store, ISecuritySettingsService settings)
    {
        _store = store;
        _settings = settings;
    }

    public async Task<bool> IsLockedAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        if (!(await _settings.GetAsync(tenantId, ct)).LockoutEnabled) return false;
        var state = await _store.GetAsync(Key(tenantId, email), ct);
        return state.LockedUntil is { } until && until > DateTimeOffset.UtcNow;
    }

    public async Task RecordFailureAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var settings = await _settings.GetAsync(tenantId, ct);
        if (!settings.LockoutEnabled) return;

        var key = Key(tenantId, email);
        var failures = await _store.RecordFailureAsync(key, FailureRetention, ct);

        var duration = LockoutPolicy.DurationFor(failures, settings.LockoutThreshold);
        if (duration > TimeSpan.Zero)
            await _store.LockAsync(key, DateTimeOffset.UtcNow.Add(duration), ct);
    }

    public Task ResetAsync(Guid tenantId, string email, CancellationToken ct = default)
        => _store.ResetAsync(Key(tenantId, email), ct);

    private static string Key(Guid tenantId, string email) => $"{tenantId}:{email.Trim().ToLowerInvariant()}";
}
