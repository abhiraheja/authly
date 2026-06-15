using Authly.Core.Security;
using StackExchange.Redis;

namespace Authly.Infrastructure.Security;

/// <summary>
/// Failed-login tracking in Redis: a TTL'd failure counter plus a lockout deadline, both keyed by
/// an opaque identity string (tenant + email). Survives across instances so lockout can't be
/// dodged by hitting another node.
/// </summary>
public sealed class RedisLoginAttemptStore : ILoginAttemptStore
{
    private const string Prefix = "lockout:";
    private readonly IConnectionMultiplexer _redis;

    public RedisLoginAttemptStore(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<LoginAttemptState> GetAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var fails = (int)await db.StringGetAsync(FailKey(key));
        var lockVal = await db.StringGetAsync(LockKey(key));
        DateTimeOffset? until = lockVal.HasValue && long.TryParse((string?)lockVal, out var unix)
            ? DateTimeOffset.FromUnixTimeSeconds(unix)
            : null;
        return new LoginAttemptState(fails, until);
    }

    public async Task<int> RecordFailureAsync(string key, TimeSpan retention, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var count = await db.StringIncrementAsync(FailKey(key));
        if (count == 1)
            await db.KeyExpireAsync(FailKey(key), retention);
        return (int)count;
    }

    public async Task LockAsync(string key, DateTimeOffset until, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var ttl = until - DateTimeOffset.UtcNow;
        if (ttl <= TimeSpan.Zero) return;
        await db.StringSetAsync(LockKey(key), until.ToUnixTimeSeconds(), ttl);
    }

    public async Task ResetAsync(string key, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        await db.KeyDeleteAsync(FailKey(key));
        await db.KeyDeleteAsync(LockKey(key));
    }

    private static RedisKey FailKey(string key) => Prefix + key + ":f";
    private static RedisKey LockKey(string key) => Prefix + key + ":l";
}
