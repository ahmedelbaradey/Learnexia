using Learnexia.Modules.Notifications.Application;
using Learnexia.Modules.Notifications.Application.Features.SendNotification;
using Learnexia.Modules.Notifications.Infrastructure;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Notifications.Api;

public static class NotificationsModule
{
    public static IServiceCollection AddNotificationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddNotificationsApplication();
        services.AddNotificationsInfrastructure(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapNotificationsModule(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/notifications").WithTags("Notifications");

        group.MapPost("/", async (SendNotificationCommand command, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(command, ct);
            return result.IsSuccess ? Results.Accepted() : Results.BadRequest(result.Error);
        });

        return endpoints;
    }
}
