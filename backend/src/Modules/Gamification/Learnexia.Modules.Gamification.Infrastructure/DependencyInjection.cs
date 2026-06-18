using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Caching;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Application.Caching.Rebuild;
using Learnexia.Modules.Gamification.Application.Features.Events.Boost;
using Learnexia.Modules.Gamification.Application.Leagues.Caching;
using Learnexia.Modules.Gamification.Infrastructure.Behaviors;
using Learnexia.Modules.Gamification.Infrastructure.Caching;
using Learnexia.Modules.Gamification.Infrastructure.Caching.Leagues;
using Learnexia.Modules.Gamification.Infrastructure.Caching.Rebuild;
using Learnexia.Modules.Gamification.Infrastructure.Features.Events.Boost;
using Learnexia.Modules.Gamification.Infrastructure.Jobs;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Gamification.Infrastructure.Queries;
using Learnexia.Modules.Gamification.Infrastructure.Queries.Cached;
using Learnexia.Modules.Gamification.Infrastructure.Repository;
using Learnexia.Modules.Gamification.Infrastructure.Service;
using Learnexia.Modules.Gamification.Infrastructure.Services;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Learnexia.Modules.Gamification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGamificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext(configuration);

        services.AddGamificationCache(configuration);

        services.AddSingleton<ILoggerManager, LoggerManager>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<IGamificationRepository, GamificationRepository>();

        // ── §7 Service layer (Handler → Service → Repository) ──────────────────────────────────
        // Handlers inject ONLY these service interfaces; the repo stays invisible to Application.
        services.AddScoped<IXpService, XpService>();
        services.AddScoped<IStreakService, StreakService>();
        services.AddScoped<IHeartService, HeartService>();
        services.AddScoped<IBadgeService, BadgeService>();
        services.AddScoped<IMissionService, MissionService>();
        services.AddScoped<ILeagueService, LeagueService>();
        services.AddScoped<IGamificationAdminService, GamificationAdminService>();
        services.AddScoped<IGamificationQueryService, GamificationQueryService>();

        // ── P4-10 Batch 1-A: decorator wiring ──────────────────────────────────────────────────
        // Each Postgres*Query is registered as its concrete type.
        // The interface is registered via a factory delegate that wraps the Postgres impl in a
        // Cached*Query decorator. Consumers (Learning dashboard) resolve the interface and get the
        // cached variant transparently — no consumer change required (Open-Closed principle).

        // XP seam (P4-02)
        services.AddScoped<PostgresStudentXpQuery>();
        services.AddScoped<IStudentXpQuery>(sp => new CachedStudentXpQuery(
            sp.GetRequiredService<PostgresStudentXpQuery>(),
            sp.GetRequiredService<IGamificationCache>(),
            sp.GetRequiredService<IOptions<GamificationCacheOptions>>(),
            sp.GetRequiredService<ILoggerManager>()));

        // Streak seam (P4-03)
        services.AddScoped<PostgresStudentStreakQuery>();
        services.AddScoped<IStudentStreakQuery>(sp => new CachedStudentStreakQuery(
            sp.GetRequiredService<PostgresStudentStreakQuery>(),
            sp.GetRequiredService<IGamificationCache>(),
            sp.GetRequiredService<IOptions<GamificationCacheOptions>>(),
            sp.GetRequiredService<ILoggerManager>()));

        // Hearts seam (P4-04) — inner has lazy-refill side effect; decorator caches post-refill snapshot.
        services.AddScoped<PostgresStudentHeartsQuery>();
        services.AddScoped<IStudentHeartsQuery>(sp => new CachedStudentHeartsQuery(
            sp.GetRequiredService<PostgresStudentHeartsQuery>(),
            sp.GetRequiredService<IGamificationCache>(),
            sp.GetRequiredService<IOptions<GamificationCacheOptions>>(),
            sp.GetRequiredService<ILoggerManager>()));

        // Badges seam (P4-05) — cache key uses fixed :3 suffix (inner hard-codes Take(3)).
        services.AddScoped<PostgresStudentBadgesQuery>();
        services.AddScoped<IStudentBadgesQuery>(sp => new CachedStudentBadgesQuery(
            sp.GetRequiredService<PostgresStudentBadgesQuery>(),
            sp.GetRequiredService<IGamificationCache>(),
            sp.GetRequiredService<IOptions<GamificationCacheOptions>>(),
            sp.GetRequiredService<ILoggerManager>()));

        // Missions seam (P4-06) — inner has lazy-instantiation side effect; cache key uses daily period key.
        services.AddScoped<PostgresStudentMissionsQuery>();
        services.AddScoped<IStudentMissionsQuery>(sp => new CachedStudentMissionsQuery(
            sp.GetRequiredService<PostgresStudentMissionsQuery>(),
            sp.GetRequiredService<IGamificationCache>(),
            sp.GetRequiredService<IOptions<GamificationCacheOptions>>(),
            sp.GetRequiredService<ISystemClock>(),
            sp.GetRequiredService<ILoggerManager>()));

        // League seam (P4-07) — inner has lazy-placement side effect; cache key uses weekly period key.
        // Batch 1-B will enhance this decorator to re-derive CurrentRank from the sorted-set (Q13).
        services.AddScoped<PostgresStudentLeagueQuery>();
        services.AddScoped<IStudentLeagueQuery>(sp => new CachedStudentLeagueQuery(
            sp.GetRequiredService<PostgresStudentLeagueQuery>(),
            sp.GetRequiredService<IGamificationCache>(),
            sp.GetRequiredService<IOptions<GamificationCacheOptions>>(),
            sp.GetRequiredService<ISystemClock>(),
            sp.GetRequiredService<ILoggerManager>()));

        // Clock seam (P4-03-B2-1): wraps DateTime.UtcNow for deterministic testing. Singleton — stateless.
        services.AddSingleton<ISystemClock, SystemClock>();

        // Hangfire sweep job (P4-03-B2-5): Transient — the job creates its own inner scope via
        // IServiceScopeFactory.CreateAsyncScope(), so it does not participate in any caller's scope.
        // Hangfire's job activator creates a fresh per-job scope anyway; Scoped here was misleading.
        services.AddTransient<StreakSweepJob>();

        // Hangfire mission-rollover job (P4-06-B4-3): Transient — mirrors StreakSweepJob registration.
        services.AddTransient<MissionRolloverJob>();

        // Hangfire league-rollover job (P4-07-B4-4): Transient — mirrors MissionRolloverJob registration.
        // Runs Monday 00:15 UTC after StreakSweepJob (00:05) and MissionRolloverJob (00:10).
        services.AddTransient<LeagueRolloverJob>();

        // Hangfire streak-at-risk + mission-reminder job (P4-09-B3-2): Transient — mirrors LeagueRolloverJob.
        // Two-pass: streak-at-risk query + daily-mission-reminder query. Runs daily at 18:00 UTC.
        services.AddTransient<StreakAtRiskJob>();

        // Hangfire lapse-win-back job (P4-09-B3-2): Transient — mirrors StreakAtRiskJob registration.
        // One-shot lapse window. Runs Sunday at 12:00 UTC.
        services.AddTransient<LapseWinBackJob>();

        // Rebuild service (P4-10-B3): Scoped — injects GamificationDbContext (also Scoped).
        // Reads all active-week LeagueMemberships from Postgres and re-seeds the Redis sorted-set
        // leaderboards. Idempotent (DEL + ZADD). Called by GamificationCacheRebuildJob.
        services.AddScoped<IGamificationCacheRebuilder, GamificationCacheRebuilder>();

        // Hangfire cache-rebuild job (P4-10-B3): Transient — mirrors other Hangfire job registrations.
        // Creates its own inner scope (via IServiceScopeFactory) per run so the Scoped rebuilder
        // is always resolved in a fresh scope. Runs daily at 03:00 UTC.
        services.AddTransient<GamificationCacheRebuildJob>();

        // ── P5-08-BE-1: XP time-series seam (Parent dashboard read) ─────────────────
        // IStudentXpTimeSeriesQuery — pure read from XpAward ledger + StudentXpProfile.
        // Scoped: depends on scoped GamificationDbContext. No cache decorator needed here
        // (time-series reads are caller-window-specific; caching is the handler's concern if needed).
        services.AddScoped<IStudentXpTimeSeriesQuery, PostgresStudentXpTimeSeriesQuery>();

        // Unit-of-Work behavior (ADR 0001 §2 + ADR 0002 §2): commit once per ICommand<>, then dispatch
        // domain events AFTER commit. Registered here in Infrastructure (not Application) because it
        // injects the concrete GamificationDbContext. Registered AFTER ValidationBehavior (added in
        // AddGamificationApplication, which is called before this) so validation rejects bad input first.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        // Badge catalog seeder (P4-05). Scoped — wired into GamificationModule.InitializeAsync in Batch 4.
        // Runs in all environments (product-as-code catalog, not demo data).
        services.AddScoped<BadgeSeeder>();

        // Mission catalog seeder (P4-06). Scoped — wired into GamificationModule.InitializeAsync in Batch 4.
        // Runs in all environments (product-as-code catalog, not demo data).
        services.AddScoped<MissionSeeder>();

        // ── P4-11-B1-B: Timed Events ─────────────────────────────────────────────────────────

        // Active timed-events query seam (P4-11-B1-B): Postgres impl wrapped by a cached decorator.
        // Consumers (Learning dashboard handler, IXpBoostCalculator in Batch 2) resolve
        // IActiveTimedEventsQuery and get the cached variant transparently (Open-Closed principle).
        services.AddScoped<PostgresActiveTimedEventsQuery>();
        services.AddScoped<IActiveTimedEventsQuery>(sp => new CachedActiveTimedEventsQuery(
            sp.GetRequiredService<PostgresActiveTimedEventsQuery>(),
            sp.GetRequiredService<IGamificationCache>(),
            sp.GetRequiredService<IOptions<GamificationCacheOptions>>(),
            sp.GetRequiredService<ILoggerManager>()));

        // Hangfire timed-event sweep job (P4-11-B1-B): Transient — mirrors StreakSweepJob registration.
        // Creates its own inner scope via IServiceScopeFactory so scoped dependencies are safe.
        services.AddTransient<TimedEventSweepJob>();

        // Timed-event catalog seeder (P4-11-B1-B). Scoped — wired into GamificationModule.InitializeAsync.
        // Seeds the WELCOME_BOOST demo row; idempotent by Code.
        services.AddScoped<TimedEventSeeder>();

        // ── P4-11-B2: XP boost calculator ────────────────────────────────────────────────────
        // GamificationEventsOptions: MaxMultiplierCeiling hard-ceiling for timed-event multipliers.
        // Bound from appsettings.json:Gamification:Events.
        services.Configure<GamificationEventsOptions>(
            configuration.GetSection(GamificationEventsOptions.SectionName));

        // FreezeOptions: config-driven dials for the streak-freeze mechanic (P4-11 Fix 3).
        // Bound from appsettings.json:Gamification:Freeze.
        services.Configure<FreezeOptions>(
            configuration.GetSection(FreezeOptions.SectionName));

        // TimedEventOptions: config-driven dials for the timed-event sweep (P4-11 Fix 4).
        // Bound from appsettings.json:Gamification:TimedEvent.
        services.Configure<TimedEventOptions>(
            configuration.GetSection(TimedEventOptions.SectionName));

        // XP boost calculator (P4-11-B2): consults active timed events via the cached
        // IActiveTimedEventsQuery seam; applies max(multipliers) capped at MaxMultiplierCeiling.
        // Scoped — shares the per-request IActiveTimedEventsQuery instance.
        services.AddScoped<IXpBoostCalculator, XpBoostCalculator>();

        // League placement service (P4-07): finds or creates the appropriate league cohort for a
        // student at a given tier + period, then stages the LeagueMembership row.
        // Scoped — owns a unit of work reference via IGamificationRepository.
        services.AddScoped<LeaguePlacementService>();

        return services;
    }

    public static IServiceCollection AddGamificationCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<GamificationCacheOptions>(
            configuration.GetSection(GamificationCacheOptions.SectionName));

        // Cache implementation: Redis when IConnectionMultiplexer is registered, Null otherwise.
        services.AddSingleton<IGamificationCache>(sp =>
        {
            var mux = sp.GetService<IConnectionMultiplexer>();
            if (mux is null)
                return new NullGamificationCache();
            return new RedisGamificationCache(
                mux,
                sp.GetRequiredService<IOptions<GamificationCacheOptions>>(),
                sp.GetRequiredService<ILoggerManager>());
        });

        // Sorted-set leaderboard (P4-10 Batch 1-B): Redis impl when IConnectionMultiplexer is
        // registered, Null impl otherwise. Singleton — composes IGamificationCache (also Singleton).
        services.AddSingleton<ILeagueLeaderboard>(sp =>
        {
            var mux = sp.GetService<IConnectionMultiplexer>();
            if (mux is null)
                return new NullLeagueLeaderboard();
            return new RedisLeagueLeaderboard(
                sp.GetRequiredService<IGamificationCache>(),
                sp.GetRequiredService<IOptions<GamificationCacheOptions>>());
        });

        return services;
    }

    public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<GamificationDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", GamificationDbContext.Schema)
                           .MigrationsAssembly(typeof(GamificationDbContext).Assembly.FullName)));
    }
}
