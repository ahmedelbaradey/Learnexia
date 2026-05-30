using Learnexia.Modules.Gamification.Api.Controllers;
using Learnexia.Modules.Gamification.Application;
using Learnexia.Modules.Gamification.Infrastructure;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Gamification.Api;

public static class GamificationModule
{
    public static IServiceCollection AddGamificationModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddGamificationApplication();
        services.AddGamificationInfrastructure(configuration);
        services.AddControllers()
            .AddApplicationPart(typeof(GamificationController).Assembly);
        return services;
    }

    public static IEndpointRouteBuilder MapGamificationModule(this IEndpointRouteBuilder endpoints) => endpoints;

    // Host-callable startup hook. Applies any pending Gamification migrations. Idempotent — MigrateAsync is a
    // no-op when the schema is up to date. The Host calls this through the module Api entry point only, so
    // the DbContext (Infrastructure) stays internal to the module (module isolation preserved). Mirrors
    // LearningModule.InitializeAsync.
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<GamificationDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
