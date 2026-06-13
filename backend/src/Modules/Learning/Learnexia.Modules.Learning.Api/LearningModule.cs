using Learnexia.Modules.Learning.Api.Controllers;
using Learnexia.Modules.Learning.Application;
using Learnexia.Modules.Learning.Infrastructure;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            .AddApplicationPart(typeof(AdaptivityController).Assembly);
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
    }
}
