using Learnexia.Modules.Notifications.Api.Controllers;
using Learnexia.Modules.Notifications.Application;
using Learnexia.Modules.Notifications.Application.Features.SendNotification;
using Learnexia.Modules.Notifications.Infrastructure;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
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
        })
        .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        // The read-side observability surface (get notifications by recipient) now lives on the MVC
        // NotificationsController (GET /api/Notifications/Notifications/List) returning the BaseResponse<T>
        // envelope — the ad-hoc minimal-API GET was removed in the Batch 3 revision.

        return endpoints;
    }

    // Host-callable startup hook. Applies any pending Notifications migrations. Idempotent — MigrateAsync is
    // a no-op when the schema is up to date. The Host calls this through the module Api entry point only, so
    // the DbContext (Infrastructure) stays internal to the module (module isolation preserved).
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<NotificationsDbContext>();
        await dbContext.Database.MigrateAsync();
    }
}
