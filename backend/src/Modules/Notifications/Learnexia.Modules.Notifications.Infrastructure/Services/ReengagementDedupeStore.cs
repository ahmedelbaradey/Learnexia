using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Learnexia.Modules.Notifications.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IReengagementDedupeStore"/>.
/// Uses atomic Redis SETNX (<c>StringSetAsync(When.NotExists)</c>) when
/// <see cref="IConnectionMultiplexer"/> is available (F-04); falls back to
/// <see cref="IDistributedCache"/> (best-effort, race-tolerant) otherwise.
/// Fail-open: any Redis exception → log WARN + return true (allow send; duplicate preferable to
/// silently dropped nudge per D4). No infrastructure details are logged (F-09).
/// </summary>
internal sealed class ReengagementDedupeStore : IReengagementDedupeStore
{
    private const int DedupeTtlHours = 36;

    // P9-08: key prefix for per-lapse-episode tier dedupe (no day component — episode TTL governs).
    private const string TierKeyPrefix = "nudge-tier:";

    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer? _redisMultiplexer;
    private readonly ILoggerManager _logger;

    public ReengagementDedupeStore(
        IDistributedCache cache,
        ILoggerManager logger,
        IConnectionMultiplexer? redisMultiplexer = null)
    {
        _cache            = cache;
        _logger           = logger;
        _redisMultiplexer = redisMultiplexer;
    }

    public async Task<bool> TryAcquireAsync(
        int studentId,
        NotificationCategory category,
        DateTime eventDay,
        CancellationToken ct = default)
    {
        var key = $"nudge:{studentId}:{(int)category}:{eventDay.Date:yyyyMMdd}";
        var ttl = TimeSpan.FromHours(DedupeTtlHours);

        try
        {
            if (_redisMultiplexer is not null)
            {
                // F-04: atomic SETNX — sets the key only if it does not already exist.
                var db = _redisMultiplexer.GetDatabase();
                return await db.StringSetAsync(key, "1", ttl, when: When.NotExists);
            }

            // Fallback: IDistributedCache (best-effort, race-tolerant but not race-free).
            var existing = await _cache.GetStringAsync(key, ct);
            if (existing is not null)
                return false;

            await _cache.SetStringAsync(key, "1",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                }, ct);
            return true;
        }
        catch (Exception ex)
        {
            // F-09: log only a safe static message — do not expose ex.Message (may contain Redis
            // connection endpoint / socket details).
            _logger.LogWarn(
                $"P4-09: Redis dedupe unavailable for key={key} — fail-open, nudge may duplicate.");
            _ = ex; // suppress unused-variable warning
            return true;
        }
    }

    /// <summary>
    /// P9-08: Per-lapse-episode tier dedupe.
    /// Key: <c>nudge-tier:{studentId}:{tierCode}</c>; TTL = episode length (e.g. 7d for gentle, 14d for repair).
    /// No day component — the tier fires at most once per episode, not once per day.
    /// Fail-open: Redis unavailable → log WARN + return true (allow send; duplicate preferable to drop).
    /// </summary>
    public async Task<bool> TryAcquireTierAsync(
        int studentId,
        string tierCode,
        TimeSpan ttl,
        CancellationToken ct = default)
    {
        var key = $"{TierKeyPrefix}{studentId}:{tierCode}";

        try
        {
            if (_redisMultiplexer is not null)
            {
                var db = _redisMultiplexer.GetDatabase();
                return await db.StringSetAsync(key, "1", ttl, when: When.NotExists);
            }

            // Fallback: IDistributedCache (best-effort, race-tolerant).
            var existing = await _cache.GetStringAsync(key, ct);
            if (existing is not null)
                return false;

            await _cache.SetStringAsync(key, "1",
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = ttl,
                }, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarn(
                $"P9-08: Redis tier dedupe unavailable for key={key} — fail-open, tier may re-fire.");
            _ = ex;
            return true;
        }
    }
}
