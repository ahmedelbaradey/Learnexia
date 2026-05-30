using System.Reflection;
using FluentValidation;
using Learnexia.Shared.Kernel.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Gamification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddGamificationApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        // MediatR is registered ONCE at the Host across ALL module Application assemblies (ADR 0002 §4)
        // so IPublisher.Publish fans out cross-module. Do NOT call AddMediatR per module.
        // Validators, AutoMapper, and the per-module ValidationBehavior stay here. The per-module
        // UnitOfWorkBehavior is registered in Infrastructure DI, AFTER this ValidationBehavior.
        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        return services;
    }
}
