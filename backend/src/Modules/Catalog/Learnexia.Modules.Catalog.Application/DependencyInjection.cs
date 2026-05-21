using System.Reflection;
using FluentValidation;
using Learnexia.Shared.Kernel.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Catalog.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        // MediatR is registered ONCE at the Host across ALL module Application assemblies (ADR 0002 §4,
        // P4-01-BE-4) so IPublisher.Publish fans out cross-module. Do NOT call AddMediatR per module.
        // Validators, AutoMapper, and the per-module ValidationBehavior stay here.
        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}
