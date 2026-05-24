using Learnexia.Modules.Parent.Api.Controllers;
using Learnexia.Modules.Parent.Application;
using Learnexia.Modules.Parent.Infrastructure;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
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

    // Host-callable startup hook. Applies any pending Parent migrations. Idempotent — MigrateAsync is a
    // no-op when the schema is up to date. The Host calls this through the module Api entry point only, so
    // the DbContext (Infrastructure) stays internal to the module (module isolation preserved). Mirrors
    // LearningModule.InitializeAsync.
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<ParentDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
