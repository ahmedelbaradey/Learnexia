using Hangfire;
using Learnexia.Modules.Billing.Application;
using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Infrastructure;
using Learnexia.Modules.Billing.Infrastructure.Jobs;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Seeders;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Learnexia.Modules.Billing.Api;

/// <summary>
/// Module entry point for the Billing module.
/// Called from the Host <c>Program.cs</c> via
/// <c>builder.Services.AddBillingModule(builder.Configuration)</c>.
///
/// <para>Mirrors <c>AiModule</c>: registers Application + Infrastructure DI,
/// exposes an <c>InitializeAsync</c> hook for EF migrations at startup.</para>
/// </summary>
public static class BillingModule
{
    public static IServiceCollection AddBillingModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddBillingApplication();
        services.AddBillingInfrastructure(configuration);
        return services;
    }

    /// <summary>
    /// Host-callable startup hook. Applies pending EF migrations for the <c>billing</c> schema.
    /// Mirrors <c>LearningModule.InitializeAsync</c> / <c>CurriculumModule.InitializeAsync</c>.
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var logger = serviceProvider.GetRequiredService<ILoggerManager>();

        try
        {
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BillingDbContext>();
            await db.Database.MigrateAsync();
            logger.LogInfo("BillingModule: migrations applied successfully (schemas: billing + platform).");

            // Seed managed GlobalSetting rows with bootstrap defaults (seed-if-absent — idempotent).
            await GlobalSettingsSeeder.SeedAsync(db, logger);

            // Clean-cutover provisioning: migrate legacy CreditAccount rows → FamilyEnergyAccount
            // wallet model. Runs AFTER MigrateAsync so schema exists. Idempotent: skips already-
            // migrated families. Uses IParentChildQuery for correct parent resolution (SECURITY HIGH-2).
            var migrationService = scope.ServiceProvider.GetRequiredService<ICreditAccountMigrationService>();
            var migrationResult  = await migrationService.MigrateAsync();
            logger.LogInfo(
                $"BillingModule: credit-account cutover — migrated={migrationResult.Migrated}, " +
                $"skipped={migrationResult.Skipped}, failed={migrationResult.Failed}.");

            if (migrationResult.Failed > 0)
                logger.LogWarn(
                    $"BillingModule: {migrationResult.Failed} family wallet(s) failed cutover provisioning. " +
                    $"Errors: {string.Join("; ", migrationResult.Errors)}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BillingModule: failed to apply migrations or seed.");
            throw;
        }

        // ── Register Hangfire recurring jobs ───────────────────────────────────────
        // AddOrUpdate is idempotent — safe to call on every startup.
        var recurringJobs = serviceProvider.GetRequiredService<IRecurringJobManager>();

        // P10-02: Monthly energy grant sweep — 01:00 UTC on the 1st of each month.
        // The cron schedule is a deployment concern; amounts come from GlobalSettings at run time.
        recurringJobs.AddOrUpdate<BillingGrantJob>(
            "billing:monthly-grant",
            job => job.RunAsync(CancellationToken.None),
            "0 1 1 * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // P10-06: Reconcile stale payments — runs every 30 minutes.
        recurringJobs.AddOrUpdate<ReconcilePaymentsJob>(
            "billing:reconcile-payments",
            job => job.RunAsync(CancellationToken.None),
            "*/30 * * * *",
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // P10-09: Dunning retry sweep — runs every hour.
        // Processes PastDue/Dunning subscriptions whose NextRetryAt has passed.
        // Config-driven retry count/intervals come from GlobalSettings at run time.
        recurringJobs.AddOrUpdate<DunningRetryJob>(
            "billing:dunning-retry",
            job => job.RunAsync(CancellationToken.None),
            "0 * * * *",   // every hour on the hour
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        // P10-15-BE-4: Seat enforcement sweep — runs daily at 02:00 UTC.
        // Locks over-limit children after grace expiry and applies RemovalScheduledAt markers.
        recurringJobs.AddOrUpdate<SeatEnforcementJob>(
            "billing:seat-enforcement",
            job => job.RunAsync(CancellationToken.None),
            "0 2 * * *",   // 02:00 UTC daily
            new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        logger.LogInfo("BillingModule: Hangfire jobs registered (billing:monthly-grant, billing:reconcile-payments, billing:dunning-retry, billing:seat-enforcement).");
    }
}
