using Learnexia.Modules.Moderation.Api.Controllers;
using Learnexia.Modules.Moderation.Application;
using Learnexia.Modules.Moderation.Infrastructure;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Moderation.Api;

public static class ModerationModule
{
    public static IServiceCollection AddModerationModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddModerationApplication();
        services.AddModerationInfrastructure(configuration);
        services.AddControllers()
            .AddApplicationPart(typeof(AuditController).Assembly);
        return services;
    }

    // Host-callable startup hook. Applies any pending Moderation migrations. Idempotent — MigrateAsync is
    // a no-op when the schema is up to date. The Host calls this through the module Api entry point only, so
    // the DbContext (Infrastructure) stays internal to the module (module isolation preserved).
    // Mirrors LearningModule.InitializeAsync and NotificationsModule.InitializeAsync.
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<ModerationDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
