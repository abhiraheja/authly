namespace Authly.Core.Security;

/// <summary>Result of a rate-limit check.</summary>
/// <param name="Allowed">False when the caller is over the limit.</param>
/// <param name="Count">Requests counted in the current window (including this one).</param>
/// <param name="RetryAfter">When blocked, roughly how long until the window resets.</param>
public readonly record struct RateLimitResult(bool Allowed, long Count, TimeSpan RetryAfter);

/// <summary>
/// A fixed-window rate limiter. Implemented over Redis in Infrastructure (atomic INCR + TTL) so
/// limits hold across instances. Keys are caller-defined (e.g. "login:ip:1.2.3.4").
/// </summary>
public interface IRateLimiter
{
    Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window, CancellationToken ct = default);
}

/// <summary>State of failed-login tracking for one identity (tenant + email).</summary>
/// <param name="Failures">Consecutive failures recorded.</param>
/// <param name="LockedUntil">When set and in the future, logins are refused until this time.</param>
public readonly record struct LoginAttemptState(int Failures, DateTimeOffset? LockedUntil);

/// <summary>
/// Tracks failed-login counters and lockout windows. Backed by Redis (with TTL) so a stolen
/// password can't be brute-forced across instances. Keyed by an opaque identity string.
/// </summary>
public interface ILoginAttemptStore
{
    Task<LoginAttemptState> GetAsync(string key, CancellationToken ct = default);

    /// <summary>Records a failure and returns the new failure count.</summary>
    Task<int> RecordFailureAsync(string key, TimeSpan retention, CancellationToken ct = default);

    /// <summary>Sets a lockout window for the key.</summary>
    Task LockAsync(string key, DateTimeOffset until, CancellationToken ct = default);

    /// <summary>Clears all failure/lockout state (e.g. after a successful login or reset).</summary>
    Task ResetAsync(string key, CancellationToken ct = default);
}

/// <summary>
/// Checks a password against a breach corpus. The HaveIBeenPwned implementation uses k-anonymity:
/// only the first 5 chars of the SHA-1 hash leave the server. Returns true if the password appears
/// in a known breach. Never throws on network failure — fails open (returns false).
/// </summary>
public interface IBreachedPasswordGateway
{
    Task<bool> IsBreachedAsync(string password, CancellationToken ct = default);
}

/// <summary>Verifies a CAPTCHA/bot-defence token against the provider (hCaptcha / Cloudflare Turnstile).</summary>
public interface ICaptchaGateway
{
    /// <summary>Verifies the client token for the given provider + secret. Returns false on any failure.</summary>
    Task<bool> VerifyAsync(string provider, string secret, string token, string? remoteIp, CancellationToken ct = default);
}

/// <summary>
/// Supplies the SHA-1 suffixes (and counts) for a 5-char hash prefix from the breach corpus.
/// Split out so the k-anonymity matching logic can be unit-tested without network.
/// </summary>
public interface IPwnedRangeClient
{
    /// <summary>Returns the raw range response ("SUFFIX:COUNT" lines) for a 5-hex-char prefix, or null on failure.</summary>
    Task<string?> GetRangeAsync(string hashPrefix, CancellationToken ct = default);
}
