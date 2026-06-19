using Learnexia.Modules.Analytics.Application;
using Learnexia.Modules.Analytics.Infrastructure;
using Learnexia.Modules.Analytics.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Analytics.Api;

public static class AnalyticsModule
{
    public static IServiceCollection AddAnalyticsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAnalyticsApplication();
        services.AddAnalyticsInfrastructure(configuration);
        // No controllers yet — controllers are added in later batches (BE-5 exposes read seams).
        // AddControllers().AddApplicationPart(...) will be added when the first controller lands.
        return services;
    }

    // Host-callable startup hook. Applies any pending Analytics migrations. Idempotent — MigrateAsync
    // is a no-op when the schema is up to date. The Host calls this through the module Api entry point
    // only, so the DbContext (Infrastructure) stays internal to the module (module isolation preserved).
    // Mirrors ModerationModule.InitializeAsync.
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<AnalyticsDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
