using System.Text.Json;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Learnexia.Modules.Ai.Infrastructure.Cache;

/// <summary>
/// Redis hot layer + PostgreSQL durable read-through implementation of
/// <see cref="IAiResponseCache"/>.
///
/// <para><strong>GET path (cache HIT contract):</strong>
/// <list type="number">
///   <item>Check Redis <c>IDistributedCache</c> by <c>"ai:rc:{cacheKey}"</c>.
///     HIT → return cached response text directly (zero DB call).</item>
///   <item>Redis MISS → query DB:
///     <c>WHERE CacheKey = key AND ReviewStatus = Approved AND InvalidatedAt IS NULL</c>.</item>
///   <item>DB HIT → populate Redis with per-intent TTL; return response text.</item>
///   <item>DB MISS → return <c>null</c> (caller proceeds to Safety Layer).</item>
/// </list>
/// </para>
///
/// <para><strong>WRITE path:</strong> DB upsert first (INSERT … ON CONFLICT DO UPDATE), then
/// populate Redis. Both steps are fail-soft — errors are logged and swallowed.</para>
///
/// <para><strong>WhyWrong LRU cap:</strong> before writing a WhyWrong entry, count existing
/// rows for the same QuestionId; if at or above cap (from
/// <see cref="IGlobalSettingsProvider"/>), evict the oldest row (by CreatedAt).</para>
///
/// <para><strong>Fail-soft invariant:</strong> any Redis or DB error → cache miss (GET) or
/// write skip (WRITE). Never throw into the AI request. Never serve unreviewed content.</para>
///
/// <para>R5 gate: only <see cref="AiCacheReviewStatus.Approved"/> + non-invalidated rows are
/// returned — enforced in the DB query predicate. Never loosen this predicate.</para>
/// </summary>
public sealed class AiResponseCacheRepository : IAiResponseCache
{
    // Redis key prefix for Ai response cache entries.
    private const string RedisKeyPrefix = "ai:rc:";

    // Default TTLs per intent (when IGlobalSettingsProvider does not override).
    private static readonly TimeSpan DefaultExplainTtl   = TimeSpan.FromHours(24);
    private static readonly TimeSpan DefaultHintTtl      = TimeSpan.FromHours(12);
    private static readonly TimeSpan DefaultWhyWrongTtl  = TimeSpan.FromHours(6);
    private static readonly TimeSpan DefaultPracticeTtl  = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly AiDbContext _dbContext;
    private readonly IDistributedCache _distributedCache;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;

    public AiResponseCacheRepository(
        AiDbContext dbContext,
        IDistributedCache distributedCache,
        IGlobalSettingsProvider settings,
        ILoggerManager logger)
    {
        _dbContext        = dbContext;
        _distributedCache = distributedCache;
        _settings         = settings;
        _logger           = logger;
    }

    /// <inheritdoc/>
    public async Task<string?> GetApprovedAsync(string cacheKey, CancellationToken cancellationToken)
    {
        // ── Step 1: Redis hot layer ──────────────────────────────────────────────
        var redisKey = RedisKeyPrefix + cacheKey;
        try
        {
            var cached = await _distributedCache.GetStringAsync(redisKey, cancellationToken);
            if (cached is not null)
            {
                _logger.LogInfo($"AiResponseCacheRepository: Redis HIT for key={cacheKey[..8]}…");
                return cached;
            }
        }
        catch (Exception ex)
        {
            // Fail-soft: Redis error → fall through to DB.
            _logger.LogWarn($"AiResponseCacheRepository: Redis GET failed — falling through to DB. {ex.GetType().Name}");
        }

        // ── Step 2: DB durable read-through ─────────────────────────────────────
        // R5 gate: only Approved + non-invalidated.
        try
        {
            var row = await _dbContext.AiResponseCaches
                .AsNoTracking()
                .Where(r =>
                    r.CacheKey == cacheKey &&
                    r.ReviewStatus == AiCacheReviewStatus.Approved &&
                    r.InvalidatedAt == null)
                .Select(r => new { r.Response, r.Type })
                .FirstOrDefaultAsync(cancellationToken);

            if (row is null)
                return null;

            _logger.LogInfo($"AiResponseCacheRepository: DB HIT for key={cacheKey[..8]}…; populating Redis.");

            // Populate Redis for future hot-layer hits.
            var ttl = GetTtlForType((AiCacheEntryType)(int)row.Type);
            await SetRedisAsync(redisKey, row.Response, ttl, cancellationToken);

            return row.Response;
        }
        catch (Exception ex)
        {
            // Fail-soft: DB error → treat as cache miss.
            _logger.LogWarn($"AiResponseCacheRepository: DB GET failed — treating as cache miss. {ex.GetType().Name}");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task WriteAsync(AiCacheWriteEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            // WhyWrong LRU cap: enforce before writing.
            if (entry.Type == AiCacheEntryTypeDto.WhyWrong && entry.QuestionId.HasValue)
                await EnforceWhyWrongCapAsync(entry.QuestionId.Value, cancellationToken);

            // DB upsert: insert or update existing row with the same CacheKey.
            var existing = await _dbContext.AiResponseCaches
                .FirstOrDefaultAsync(r => r.CacheKey == entry.CacheKey, cancellationToken);

            if (existing is null)
            {
                var newRow = new AiResponseCache
                {
                    CacheKey          = entry.CacheKey,
                    Response          = entry.Response,
                    Type              = (AiCacheEntryType)(int)entry.Type,
                    SkillKey          = entry.SkillKey,
                    QuestionId        = entry.QuestionId,
                    CurriculumVersion = entry.CurriculumVersion,
                    PromptVersion     = entry.PromptVersion,
                    ModelVersion      = entry.ModelVersion,
                    ReviewStatus      = (AiCacheReviewStatus)(int)entry.ReviewStatus,
                    Confidence        = entry.Confidence,
                    CreatedAt         = DateTime.UtcNow,
                };
                _dbContext.AiResponseCaches.Add(newRow);
            }
            else
            {
                // Update to latest response + review status (e.g. approval threshold change).
                existing.Response      = entry.Response;
                existing.ReviewStatus  = (AiCacheReviewStatus)(int)entry.ReviewStatus;
                existing.Confidence    = entry.Confidence;
                existing.ModelVersion  = entry.ModelVersion;
                existing.PromptVersion = entry.PromptVersion;
                existing.InvalidatedAt = null; // Clear any prior invalidation.
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // Populate Redis when the entry is Approved (only approved entries get hot-layer hits).
            if (entry.ReviewStatus == AiCacheReviewStatusDto.Approved)
            {
                var redisKey = RedisKeyPrefix + entry.CacheKey;
                var ttl = GetTtlForType((AiCacheEntryType)(int)entry.Type);
                await SetRedisAsync(redisKey, entry.Response, ttl, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            // Fail-soft: write errors never propagate to the handler.
            _logger.LogWarn($"AiResponseCacheRepository: WriteAsync failed — write skipped. {ex.GetType().Name}");
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Enforces the WhyWrong variant cap for a given QuestionId (LRU eviction by CreatedAt).
    /// </summary>
    private async Task EnforceWhyWrongCapAsync(int questionId, CancellationToken cancellationToken)
    {
        var cap = _settings.GetInt("ai.cache.whyWrongVariantCap", 50);
        if (cap <= 0) return;

        var count = await _dbContext.AiResponseCaches
            .CountAsync(r =>
                r.QuestionId == questionId &&
                r.Type == AiCacheEntryType.WhyWrong &&
                r.InvalidatedAt == null,
                cancellationToken);

        if (count < cap) return;

        // Evict oldest (LRU by CreatedAt) — remove (count - cap + 1) to make room.
        var toEvict = count - cap + 1;
        var oldest = await _dbContext.AiResponseCaches
            .Where(r =>
                r.QuestionId == questionId &&
                r.Type == AiCacheEntryType.WhyWrong &&
                r.InvalidatedAt == null)
            .OrderBy(r => r.CreatedAt)
            .Take(toEvict)
            .ToListAsync(cancellationToken);

        foreach (var row in oldest)
            row.InvalidatedAt = DateTime.UtcNow;

        _logger.LogInfo($"AiResponseCacheRepository: LRU evicted {oldest.Count} WhyWrong entries for questionId={questionId}.");
    }

    /// <summary>Writes a value to Redis with a TTL; swallows errors (fail-soft).</summary>
    private async Task SetRedisAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        try
        {
            var options = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl };
            await _distributedCache.SetStringAsync(key, value, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarn($"AiResponseCacheRepository: Redis SET failed — hot-layer miss on next request. {ex.GetType().Name}");
        }
    }

    /// <summary>Returns the per-intent Redis TTL (using defaults; config-override can be added via IGlobalSettingsProvider in P10-12).</summary>
    private static TimeSpan GetTtlForType(AiCacheEntryType type) => type switch
    {
        AiCacheEntryType.Explain  => DefaultExplainTtl,
        AiCacheEntryType.Hint     => DefaultHintTtl,
        AiCacheEntryType.WhyWrong => DefaultWhyWrongTtl,
        AiCacheEntryType.Practice => DefaultPracticeTtl,
        _                         => DefaultExplainTtl,
    };
}
