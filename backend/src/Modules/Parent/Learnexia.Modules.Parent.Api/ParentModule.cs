using Hangfire;
using Learnexia.Modules.Parent.Api.Controllers;
using Learnexia.Modules.Parent.Application;
using Learnexia.Modules.Parent.Infrastructure;
using Learnexia.Modules.Parent.Infrastructure.Jobs;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Parent.Api;

public static class ParentModule
{
    public static IServiceCollection AddParentModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddParentApplication();
        services.AddParentInfrastructure(configuration);
        services.AddControllers()
            .AddApplicationPart(typeof(ParentController).Assembly);
        return services;
    }

    public static IEndpointRouteBuilder MapParentModule(this IEndpointRouteBuilder endpoints) => endpoints;

    /// <summary>
    /// Host-callable startup hook. Applies any pending Parent migrations and registers
    /// the Hangfire recurring jobs. Idempotent — <c>MigrateAsync</c> is a no-op when
    /// the schema is up to date; <c>AddOrUpdate</c> is idempotent per job id.
    ///
    /// Mirrors <c>BillingModule.InitializeAsync</c> for the Hangfire registration pattern.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerManager>();

        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ParentDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInfo("ParentModule: migrations applied successfully (schema: parent).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "ParentModule: failed to apply migrations.");
            throw;
        }

        // ── Register Hangfire recurring jobs ───────────────────────────────────────────
        // AddOrUpdate is idempotent — safe to call on every startup.
        var recurringJobs = serviceProvider.GetRequiredService<IRecurringJobManager>();

        // P5-01-BE-3: Weekly report generator — runs every Monday at 03:00 UTC.
        // The prior-week window is computed inside the job from DateTime.UtcNow.
        recurringJobs.AddOrUpdate<WeeklyReportJob>(
            "parent:weekly-report",
            job => job.RunAsync(CancellationToken.None),
            "0 3 * * 1",   // 03:00 UTC every Monday
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        logger.LogInfo("ParentModule: Hangfire jobs registered (parent:weekly-report).");
    }
}
