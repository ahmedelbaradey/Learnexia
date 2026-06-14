using Hangfire;
using Learnexia.Modules.Learning.Api.Controllers;
using Learnexia.Modules.Learning.Application;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Modules.Learning.Infrastructure;
using Learnexia.Modules.Learning.Infrastructure.Jobs;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Learning.Api;

public static class LearningModule
{
    public static IServiceCollection AddLearningModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddLearningApplication();
        services.AddLearningInfrastructure(configuration);
        services.AddControllers()
            .AddApplicationPart(typeof(GradesController).Assembly)
            .AddApplicationPart(typeof(SubjectsController).Assembly)
            .AddApplicationPart(typeof(UnitsController).Assembly)
            .AddApplicationPart(typeof(LessonsController).Assembly)
            .AddApplicationPart(typeof(ConceptsController).Assembly)
            .AddApplicationPart(typeof(SkillsController).Assembly)
            .AddApplicationPart(typeof(QuizzesController).Assembly)
            .AddApplicationPart(typeof(AdaptivityController).Assembly)
            // ProfileController is in the same Learnexia.Modules.Learning.Api assembly as
            // GradesController — no additional AddApplicationPart needed; it is already covered.
            .AddApplicationPart(typeof(ReviewsController).Assembly);
        return services;
    }

    public static IEndpointRouteBuilder MapLearningModule(this IEndpointRouteBuilder endpoints) => endpoints;

    // Host-callable startup hook. Applies any pending Learning migrations. Idempotent — MigrateAsync is a
    // no-op when the schema is up to date. The Host calls this through the module Api entry point only, so
    // the DbContext (Infrastructure) stays internal to the module (module isolation preserved). Mirrors
    // CatalogModule.InitializeAsync.
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<LearningDbContext>();
        await dbContext.Database.MigrateAsync();

        // Seed demo curriculum data in Development only — mirrors the IdentitySeeder dev-only gate.
        // The environment check lives here (not in LearningSeeder itself) so the seeder stays
        // environment-neutral and unit tests can call it directly without a host environment.
        var env = serviceProvider.GetRequiredService<IHostEnvironment>();
        if (env.IsDevelopment())
            await LearningSeeder.SeedAsync(serviceProvider);

        // Register the daily spaced-repetition sweep recurring job (P3-10, AC3).
        // IRecurringJobManager is available because Hangfire.Core is wired by the Host (P1-07).
        // AddOrUpdate is idempotent — safe to call on every startup (redeploys won't double-register).
        // Fixed ID "SR-Sweep" — Hangfire dedupes by ID (idempotency requirement, AC3).
        var srOptions      = serviceProvider.GetRequiredService<IOptions<SpacedRepetitionOptions>>().Value;
        var recurringJobs  = serviceProvider.GetRequiredService<IRecurringJobManager>();

        recurringJobs.AddOrUpdate<SpacedRepetitionSweepJob>(
            "SR-Sweep",
            job => job.RunAsync(CancellationToken.None),
            srOptions.JobCronExpression,
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // Register the daily student-profile recompute job (P3-13, AC2).
        // Fixed ID "SP-Recompute" — Hangfire dedupes by ID (idempotency requirement).
        // Runs daily at midnight UTC (Cron.Daily = "0 0 * * *").
        recurringJobs.AddOrUpdate<StudentProfileRecomputeJob>(
            "SP-Recompute",
            job => job.Execute(CancellationToken.None),
            Cron.Daily(),
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });
    }
}
