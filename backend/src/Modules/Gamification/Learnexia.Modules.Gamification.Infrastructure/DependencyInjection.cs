using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Infrastructure.Behaviors;
using Learnexia.Modules.Gamification.Infrastructure.Jobs;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Gamification.Infrastructure.Queries;
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

namespace Learnexia.Modules.Gamification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGamificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext(configuration);

        services.AddSingleton<ILoggerManager, LoggerManager>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<IGamificationRepository, GamificationRepository>();

        // Cross-module read seam: Learning dashboard injects IStudentXpQuery to read XP without
        // referencing GamificationDbContext directly (module isolation rule 1). Mirrors IParentChildQuery.
        // Future P4-10 swaps this for a Redis-backed implementation behind the same seam.
        services.AddScoped<IStudentXpQuery, StudentXpQuery>();

        // Cross-module streak read seam (P4-03): Learning dashboard injects IStudentStreakQuery to read
        // streak state without referencing GamificationDbContext directly (module isolation rule 1).
        services.AddScoped<IStudentStreakQuery, StudentStreakQuery>();

        // Cross-module hearts read seam (P4-04): Learning dashboard injects IStudentHeartsQuery to read
        // hearts state without referencing GamificationDbContext directly (module isolation rule 1).
        // Includes persist-on-read for lazy refill (D5 / Q1.bis).
        services.AddScoped<IStudentHeartsQuery, StudentHeartsQuery>();

        // Cross-module badges read seam (P4-05): Learning dashboard injects IStudentBadgesQuery to read
        // badge count + recent-3 without referencing GamificationDbContext directly (module isolation rule 1).
        // Returns sentinel (0, []) for brand-new students — never null (D2).
        services.AddScoped<IStudentBadgesQuery, StudentBadgesQuery>();

        // Cross-module missions read seam (P4-06): Learning dashboard injects IStudentMissionsQuery to read
        // current-period missions without referencing GamificationDbContext directly (module isolation rule 1).
        // Lazy-instantiates the current period's missions on first call per period (D2 decision).
        // Returns sentinel ([], null) for brand-new students — never null.
        services.AddScoped<IStudentMissionsQuery, StudentMissionsQuery>();

        // Cross-module league read seam (P4-07): Learning dashboard injects IStudentLeagueQuery to read
        // current-week league snapshot without referencing GamificationDbContext directly (module isolation rule 1).
        // Lazy-instantiates the current-week league membership on first call per period (D12 / AC1).
        // Returns sentinel (Bronze, 0, 0, 0) for brand-new students with no profile — never null (D13).
        services.AddScoped<IStudentLeagueQuery, StudentLeagueQuery>();

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

        // League placement service (P4-07): finds or creates the appropriate league cohort for a
        // student at a given tier + period, then stages the LeagueMembership row.
        // Scoped — owns a unit of work reference via IGamificationRepository.
        services.AddScoped<LeaguePlacementService>();

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
