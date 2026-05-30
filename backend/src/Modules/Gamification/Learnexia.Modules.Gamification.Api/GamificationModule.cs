using Hangfire;
using Learnexia.Modules.Gamification.Api.Controllers;
using Learnexia.Modules.Gamification.Application;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Modules.Gamification.Infrastructure;
using Learnexia.Modules.Gamification.Infrastructure.Jobs;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Gamification.Api;

public static class GamificationModule
{
    public static IServiceCollection AddGamificationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGamificationApplication(configuration);
        services.AddGamificationInfrastructure(configuration);
        services.AddControllers()
            .AddApplicationPart(typeof(GamificationController).Assembly);
        return services;
    }

    public static IEndpointRouteBuilder MapGamificationModule(this IEndpointRouteBuilder endpoints) => endpoints;

    /// <summary>
    /// Host-callable startup hook. Applies any pending Gamification migrations and registers
    /// the streak sweep recurring job with Hangfire. Idempotent — MigrateAsync and
    /// <c>RecurringJob.AddOrUpdate</c> are both no-ops when already up to date. Mirrors
    /// <c>LearningModule.InitializeAsync</c>.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<GamificationDbContext>();
        await dbContext.Database.MigrateAsync();

        // Register the daily streak sweep recurring job (P4-03-B2-8, D4).
        // IRecurringJobManager is available because Hangfire.Core is wired by the Host (P1-07).
        // AddOrUpdate is idempotent — safe to call on every startup (redeploys won't double-register).
        var streakOptions = serviceProvider.GetRequiredService<IOptions<StreakOptions>>().Value;
        var recurringJobs = serviceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobs.AddOrUpdate<StreakSweepJob>(
            "gamification:streak-sweep",
            job => job.RunAsync(CancellationToken.None),
            streakOptions.DailyJobCron,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
