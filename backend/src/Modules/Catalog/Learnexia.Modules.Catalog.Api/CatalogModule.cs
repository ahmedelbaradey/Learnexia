using Learnexia.Modules.Catalog.Api.Controllers;
using Learnexia.Modules.Catalog.Application;
using Learnexia.Modules.Catalog.Infrastructure;
using Learnexia.Modules.Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Catalog.Api;

public static class CatalogModule
{
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCatalogApplication();
        services.AddCatalogInfrastructure(configuration);
        services.AddControllers()
        .AddApplicationPart(typeof(CategoriesController).Assembly)
        .AddApplicationPart(typeof(ProductsController).Assembly);
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogModule(this IEndpointRouteBuilder endpoints) => endpoints;

    // Host-callable startup hook. Applies any pending Catalog migrations. Idempotent — MigrateAsync is a
    // no-op when the schema is up to date. The Host calls this through the module Api entry point only, so
    // the DbContext (Infrastructure) stays internal to the module (module isolation preserved).
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<CatalogDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
