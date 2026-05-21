using Learnexia.Modules.Catalog.Api.Controllers;
using Learnexia.Modules.Catalog.Application;
using Learnexia.Modules.Catalog.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
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
}
