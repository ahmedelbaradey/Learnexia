using System.Text.Json;
using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Learnexia.Modules.Gamification.Infrastructure.Caching;

/// <summary>
/// Fail-soft Redis implementation. Every operation is wrapped in a try/catch on
/// RedisException; failures log a single WARN line and return null/false/no-op so the
/// caller falls through to Postgres. The Enabled flag short-circuits everything to a
/// no-op (kill-switch for ops emergencies).
/// </summary>
internal sealed class RedisGamificationCache : IGamificationCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly GamificationCacheOptions _options;
    private readonly ILoggerManager _logger;

    public RedisGamificationCache(
        IConnectionMultiplexer redis,
        IOptions<GamificationCacheOptions> options,
        ILoggerManager logger)
    {
        _redis = redis;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<T?> TryGetAsync<T>(string key, CancellationToken ct) where T : class
    {
        if (!_options.Enabled) return null;
        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (RedisException)
        {
            _logger.LogWarn("P4-10: Redis TryGet failed — Postgres fallthrough.");
            return null;
        }
        catch (JsonException)
        {
            _logger.LogWarn("P4-10: Redis cache deserialize failed — value treated as miss.");
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan ttl, CancellationToken ct) where T : class
    {
        if (!_options.Enabled) return;
        try
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, json, ttl);
        }
        catch (RedisException)
        {
            _logger.LogWarn("P4-10: Redis Set failed — write skipped.");
        }
        catch (JsonException)
        {
            _logger.LogWarn("P4-10: Redis cache serialize failed — write skipped.");
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        if (!_options.Enabled) return;
        try
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(key);
        }
        catch (RedisException)
        {
            _logger.LogWarn("P4-10: Redis Delete failed — key may persist until TTL.");
        }
    }

    public async Task SortedSetUpsertAsync(string key, long score, string member, TimeSpan? expire, CancellationToken ct)
    {
        if (!_options.Enabled) return;
        try
        {
            var db = _redis.GetDatabase();
            // ZADD CH semantics — returns count of changed entries; we ignore it.
            await db.SortedSetAddAsync(key, member, score, When.Always, CommandFlags.None);
            if (expire is { } ttl)
                await db.KeyExpireAsync(key, ttl);
        }
        catch (RedisException)
        {
            _logger.LogWarn("P4-10: Redis ZADD failed — sorted set may drift until rebuild.");
        }
    }

    public async Task<long?> SortedSetGetRankAsync(string key, string member, bool descending, CancellationToken ct)
    {
        if (!_options.Enabled) return null;
        try
        {
            var db = _redis.GetDatabase();
            var order = descending ? Order.Descending : Order.Ascending;
            var rank = await db.SortedSetRankAsync(key, member, order);
            return rank;
        }
        catch (RedisException)
        {
            _logger.LogWarn("P4-10: Redis ZRANK failed — null rank returned.");
            return null;
        }
    }

    public async Task<long> SortedSetCardAsync(string key, CancellationToken ct)
    {
        if (!_options.Enabled) return 0;
        try
        {
            var db = _redis.GetDatabase();
            return await db.SortedSetLengthAsync(key);
        }
        catch (RedisException)
        {
            _logger.LogWarn("P4-10: Redis ZCARD failed — 0 returned.");
            return 0;
        }
    }

    public async Task<IReadOnlyList<(string member, double score)>> SortedSetRangeAsync(
        string key, long start, long stop, bool descending, CancellationToken ct)
    {
        if (!_options.Enabled) return Array.Empty<(string, double)>();
        try
        {
            var db = _redis.GetDatabase();
            var order = descending ? Order.Descending : Order.Ascending;
            var entries = await db.SortedSetRangeByRankWithScoresAsync(key, start, stop, order);
            return entries.Select(e => (e.Element.ToString(), e.Score)).ToArray();
        }
        catch (RedisException)
        {
            _logger.LogWarn("P4-10: Redis ZRANGE failed — empty list returned.");
            return Array.Empty<(string, double)>();
        }
    }

    public async Task<double?> SortedSetGetScoreAsync(string key, string member, CancellationToken ct)
    {
        if (!_options.Enabled) return null;
        try
        {
            var db = _redis.GetDatabase();
            return await db.SortedSetScoreAsync(key, member);
        }
        catch (RedisException)
        {
            _logger.LogWarn("P4-10: Redis ZSCORE failed — null score returned.");
            return null;
        }
    }
}
