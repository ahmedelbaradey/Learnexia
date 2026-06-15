using Learnexia.Modules.Billing.Application;
using Learnexia.Modules.Billing.Infrastructure;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Seeders;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "BillingModule: failed to apply migrations or seed.");
            throw;
        }
    }
}
