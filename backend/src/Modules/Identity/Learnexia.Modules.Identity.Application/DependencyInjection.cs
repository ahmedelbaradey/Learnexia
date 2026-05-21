using System.Reflection;
using FluentValidation;
using Learnexia.Shared.Kernel.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        // MediatR is registered ONCE at the Host across ALL module Application assemblies (ADR 0002 §4,
        // P4-01-BE-4) so IPublisher.Publish fans out cross-module. Do NOT call AddMediatR per module —
        // a second AddMediatR re-registers IMediator/ISender/IPublisher and only the last scan wins,
        // silently dropping the other modules' handlers. Validators, AutoMapper, and the per-module
        // ValidationBehavior stay here.
        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
