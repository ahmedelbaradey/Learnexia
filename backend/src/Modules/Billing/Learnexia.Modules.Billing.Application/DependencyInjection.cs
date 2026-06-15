using System.Reflection;
using FluentValidation;
using Learnexia.Shared.Kernel.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Billing.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR handlers — registered by the host's cross-module AddCrossModuleMediatR call.
        // Register here for module-isolated or test scenarios.
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));

        // FluentValidation validators — auto-discovered by ValidationBehavior.
        services.AddValidatorsFromAssembly(assembly);

        // AutoMapper profiles (BillingProfile).
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));

        // ValidationBehavior — commands only (ICommand<>).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
