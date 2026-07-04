using Authly.Core.Security;
using StackExchange.Redis;

namespace Authly.Infrastructure.Security;

/// <summary>
/// Fixed-window rate limiter over Redis: an atomic INCR per key, with the window's TTL set on the
/// first hit. Shared across instances, so a flood can't be spread thin by load balancing.
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter
{
    private const string Prefix = "rl:";
    private readonly IConnectionMultiplexer _redis;

    public RedisRateLimiter(IConnectionMultiplexer redis) => _redis = redis;

    public async Task<RateLimitResult> CheckAsync(string key, int limit, TimeSpan window, CancellationToken ct = default)
    {
        var db = _redis.GetDatabase();
        var redisKey = Prefix + key;

        var count = await db.StringIncrementAsync(redisKey);

        // (Re)arm the window whenever the key has no TTL — not just on the first
        // hit. If the EXPIRE after the first INCR was ever missed (crash, timeout,
        // failover), the counter would otherwise live forever and every request
        // from that key would 429 permanently.
        var ttl = await db.KeyTimeToLiveAsync(redisKey);
        if (ttl is null)
        {
            await db.KeyExpireAsync(redisKey, window);
            ttl = window;
        }

        return new RateLimitResult(count <= limit, count, ttl.Value);
    }
}
