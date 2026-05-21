using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        // MediatR is registered ONCE at the Host across ALL module Application assemblies (ADR 0002 §4,
        // P4-01-BE-4) so IPublisher.Publish fans out cross-module — this is what lets the Notifications
        // INotificationHandler<>s receive integration events produced by other modules. Do NOT call
        // AddMediatR per module. Validators stay here.
        services.AddValidatorsFromAssembly(assembly);
        return services;
    }
}
