using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;

namespace Learnexia.Modules.Notifications.Infrastructure.Reengagement;

/// <summary>
/// Infrastructure implementation of <see cref="INudgeArbiter"/> (P9-07).
///
/// Decision pipeline (for the PUSH channel only — in-app inbox is never rationed):
/// 1. Derive the cross-category push count from DB inbox rows (authoritative; Redis-outage-safe).
/// 2. Check per-type cooldown via Redis SETNX (fail-open: outage → cooldown not enforced).
/// 3. Derive priority rank from config-driven order; apply the <see cref="ReengagementEvaluator.ArbitratePush"/> pure gate.
/// 4. On GRANT: set the cooldown key (SETNX) so this type cannot fire again within its TTL.
///
/// <para>No-batching / emergent priority: handlers fire independently per integration event.
/// Priority is realised emergently — higher-priority types consume the budget first; lower-priority
/// types encounter GlobalBudgetExhausted later in the day. See <see cref="INudgeArbiter"/> for details.</para>
///
/// Scoped lifetime — registered alongside <see cref="INudgeDispatcher"/> in <c>AddNotificationsInfrastructure</c>.
/// </summary>
internal sealed class NudgeArbiter : INudgeArbiter
{
    // ── Config keys (tunable via IGlobalSettingsProvider without deploy) ─────────────────────────
    private const string PriorityOrderKey       = "Notifications:PriorityOrder";
    private const string CooldownKeyPrefix      = "Notifications:Cooldown:";
    private const string DefaultPriorityOrder   = "StreakAtRisk,DailyMission,WeeklyChallenge,LapseWinBack,Achievement,WeeklyReport";

    // Default per-type cooldown TTLs in hours (24h = 1/day, 168h = 1/week)
    private const int DefaultCooldownHours = 24;
    private const int WeeklyCooldownHours  = 168;

    // Redis key shapes:
    //   cooldown:{childId}:{typeCode}  — SETNX, per-type TTL
    private const string CooldownRedisKeyTemplate = "cooldown:{0}:{1}";

    private readonly INotificationInboxService    _inboxService;
    private readonly IGlobalSettingsProvider      _settings;
    private readonly IConnectionMultiplexer?      _redisMultiplexer;
    private readonly IDistributedCache            _cache;
    private readonly ILoggerManager               _logger;

    public NudgeArbiter(
        INotificationInboxService inboxService,
        IGlobalSettingsProvider settings,
        ILoggerManager logger,
        IDistributedCache cache,
        IConnectionMultiplexer? redisMultiplexer = null)
    {
        _inboxService     = inboxService;
        _settings         = settings;
        _logger           = logger;
        _cache            = cache;
        _redisMultiplexer = redisMultiplexer;
    }

    public async Task<ReengagementEvaluator.ArbiterResult> ArbitrateAsync(
        int childId,
        NotificationCategory category,
        string typeCode,
        int globalBudget,
        DateTime nowUtc,
        CancellationToken ct = default)
    {
        // ── Step 1: DB-derived cross-category push count (authoritative, Redis-outage-safe) ─────
        var pushesSentToday = await _inboxService.CountPushesSentTodayAsync(childId, nowUtc, ct);

        // ── Step 2: Per-type cooldown via Redis SETNX (fail-open) ───────────────────────────────
        var cooldownKey    = string.Format(CooldownRedisKeyTemplate, childId, typeCode);
        var cooldownActive = await IsCooldownActiveAsync(cooldownKey, ct);

        // ── Step 3: Priority rank from config-driven order ───────────────────────────────────────
        var priorityRank = GetPriorityRank(category);
        // Remaining budget after accounting for current state. Pass the raw remaining budget —
        // the emergent model means we don't pre-reserve slots for future higher-priority types;
        // we only suppress if the budget is already depleted or this type lost a slot reservation.
        var budgetRemaining = globalBudget - pushesSentToday;

        // ── Step 4: Pure gate (allocation-free, unit-testable) ───────────────────────────────────
        var result = ReengagementEvaluator.ArbitratePush(
            pushesSentToday,
            globalBudget,
            cooldownActive,
            priorityRank,
            budgetRemaining);

        if (!result.ShouldPush)
        {
            _logger.LogInfo(
                $"analytics.reengagement.push_suppressed reason={result.SuppressReason} " +
                $"category={category} typeCode={typeCode} childId={childId} " +
                $"pushesSentToday={pushesSentToday} globalBudget={globalBudget}");
            return result;
        }

        // ── Step 5: GRANT — set cooldown so this type can't fire again within its TTL ───────────
        await TrySetCooldownAsync(cooldownKey, typeCode, ct);

        return result;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Checks whether a cooldown key already exists in Redis (SETNX-style check).
    /// Returns false (no cooldown) on Redis outage — fail-open consistent with <c>ReengagementDedupeStore</c>.
    /// </summary>
    private async Task<bool> IsCooldownActiveAsync(string cooldownKey, CancellationToken ct)
    {
        try
        {
            if (_redisMultiplexer is not null)
            {
                var db = _redisMultiplexer.GetDatabase();
                return await db.KeyExistsAsync(cooldownKey);
            }

            // Fallback: IDistributedCache
            var existing = await _cache.GetStringAsync(cooldownKey, ct);
            return existing is not null;
        }
        catch
        {
            // Fail-open: Redis unavailable → treat as no cooldown (allow send).
            _logger.LogWarn(
                $"P9-07: Redis cooldown check unavailable for key={cooldownKey} — fail-open, cooldown not enforced.");
            return false;
        }
    }

    /// <summary>
    /// Sets the cooldown key (SETNX) after a granted push, so this type cannot fire again within its TTL.
    /// Fail-soft: Redis failure does not block the send — just means the next dispatch won't see the cooldown.
    /// </summary>
    private async Task TrySetCooldownAsync(string cooldownKey, string typeCode, CancellationToken ct)
    {
        try
        {
            var ttl = GetCooldownTtl(typeCode);

            if (_redisMultiplexer is not null)
            {
                var db = _redisMultiplexer.GetDatabase();
                await db.StringSetAsync(cooldownKey, "1", ttl, when: When.NotExists);
                return;
            }

            // Fallback: IDistributedCache
            await _cache.SetStringAsync(cooldownKey, "1",
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl }, ct);
        }
        catch
        {
            _logger.LogWarn(
                $"P9-07: Redis cooldown set unavailable for key={cooldownKey} — cooldown not recorded; next send may bypass TTL.");
        }
    }

    /// <summary>
    /// Returns the priority rank for <paramref name="category"/>: 0 = highest priority.
    /// Config key: <c>Notifications:PriorityOrder</c> (CSV of category names, highest first).
    /// Default: StreakAtRisk,DailyMission,WeeklyChallenge,LapseWinBack,Achievement,WeeklyReport
    /// Categories not listed get the highest-available rank (treated as lowest priority).
    /// </summary>
    private int GetPriorityRank(NotificationCategory category)
    {
        var orderCsv = _settings.GetString(PriorityOrderKey, DefaultPriorityOrder);
        var parts    = orderCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (var i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], category.ToString(), StringComparison.OrdinalIgnoreCase))
                return i;
        }

        // Not found → lowest priority (highest rank number).
        return parts.Length;
    }

    /// <summary>
    /// Returns the cooldown TTL for a given <paramref name="typeCode"/> from config, or a
    /// sensible per-type default. Config key: <c>Notifications:Cooldown:{typeCode}</c> (hours).
    /// </summary>
    private TimeSpan GetCooldownTtl(string typeCode)
    {
        var configKey = $"{CooldownKeyPrefix}{typeCode}";
        var defaultHours = typeCode switch
        {
            "LEAGUE_TIER_CHANGED"     => DefaultCooldownHours,   // ≤1/day
            "LEVELED_UP"              => DefaultCooldownHours,   // ≤1/day
            "STREAK_FREEZE_CONSUMED"  => DefaultCooldownHours,   // ≤1/day
            "DAILY_MISSION_REMINDER"  => DefaultCooldownHours,   // ≤1/day
            "LAPSE_WIN_BACK"          => DefaultCooldownHours,   // ≤1/day (per-episode dedupe handles longer)
            "LAPSE_WIN_BACK_GENTLE"   => DefaultCooldownHours,
            "LAPSE_WIN_BACK_REPAIR"   => DefaultCooldownHours,
            "LAPSE_WIN_BACK_FRESH_START" => DefaultCooldownHours,
            "WEEKLY_RECAP"                => WeeklyCooldownHours,    // ≤1/week
            "WEEKLY_CHALLENGE_REMINDER"   => WeeklyCooldownHours,    // ≤1/week (Option-A dedupe is primary guard; cooldown backs it up for push channel)
            _                             => DefaultCooldownHours,
        };

        var hours = _settings.GetInt(configKey, defaultHours);
        return TimeSpan.FromHours(hours);
    }
}
