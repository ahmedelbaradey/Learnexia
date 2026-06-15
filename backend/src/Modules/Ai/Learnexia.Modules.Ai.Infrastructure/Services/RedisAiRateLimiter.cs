using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Kernel.Abstractions;
using StackExchange.Redis;

namespace Learnexia.Modules.Ai.Infrastructure.Services;

/// <summary>
/// Redis-backed per-student fixed-window AI rate limiter (WI-B5).
///
/// <para>Uses atomic <c>INCR</c> + <c>EXPIRE</c> on <c>IConnectionMultiplexer</c> for
/// correctness under concurrency across multiple app instances. Counter key:
/// <c>"ai:rl:{studentId}:{windowMinute}"</c> where <c>windowMinute</c> is the UTC minute
/// epoch (floor(UnixSeconds / 60)).</para>
///
/// <para><strong>Fail-soft:</strong> any <c>RedisException</c> → log + return <c>true</c>
/// (allow the request) so a Redis outage never hard-blocks students from getting AI help.
/// This mirrors the Gamification Redis/Null pattern.</para>
///
/// <para>Registered as a Singleton via the Redis/Null selector in
/// <c>AddAiInfrastructure</c>. When Redis is absent, <c>AiTutorRateLimiter</c>
/// (in-process <c>ConcurrentDictionary</c>) is registered instead.</para>
/// </summary>
public sealed class RedisAiRateLimiter : IAiTutorRateLimiter
{
    // Fixed-window — intentionally conservative for MVP (mirrors in-process defaults).
    private const int MaxRequestsPerWindow = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILoggerManager _logger;

    public RedisAiRateLimiter(IConnectionMultiplexer redis, ILoggerManager logger)
    {
        _redis  = redis;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool TryAllow(int studentId)
    {
        try
        {
            var db = _redis.GetDatabase();

            // Window key: floor to the current 60-second bucket so all instances share state.
            var windowBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 60;
            var key = $"ai:rl:{studentId}:{windowBucket}";

            // Atomic INCR: returns the new counter value after increment.
            var count = db.StringIncrement(key);

            // Set expiry only on first increment to avoid resetting the window on every call.
            if (count == 1)
                db.KeyExpire(key, Window);

            return count <= MaxRequestsPerWindow;
        }
        catch (RedisException ex)
        {
            // Fail-soft: Redis unavailable → allow the request (graceful degrade).
            _logger.LogWarn($"RedisAiRateLimiter: Redis INCR failed for studentId={studentId} — allowing request. {ex.GetType().Name}");
            return true;
        }
        catch (Exception ex)
        {
            // Catch-all: unexpected error → allow (fail-open for rate limiter, not safety).
            _logger.LogWarn($"RedisAiRateLimiter: unexpected error for studentId={studentId} — allowing request. {ex.GetType().Name}");
            return true;
        }
    }
}
