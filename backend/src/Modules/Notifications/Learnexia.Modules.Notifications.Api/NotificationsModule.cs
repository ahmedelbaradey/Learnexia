using Learnexia.Modules.Notifications.Api.Controllers;
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
        services.AddControllers()
            .AddApplicationPart(typeof(NotificationsController).Assembly);
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

        // The read-side observability surface (get notifications by recipient) now lives on the MVC
        // NotificationsController (GET /api/Notifications/Notifications/List) returning the BaseResponse<T>
        // envelope — the ad-hoc minimal-API GET was removed in the Batch 3 revision.

        return endpoints;
    }
}
