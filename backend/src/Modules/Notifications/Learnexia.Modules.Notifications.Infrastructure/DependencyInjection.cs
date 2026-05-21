using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>(opt =>
            opt.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
                .UseNpgsql(configuration.GetConnectionString("Default"),
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", NotificationsDbContext.Schema)));

        services.AddScoped<INotificationsDbContext>(sp => sp.GetRequiredService<NotificationsDbContext>());

        return services;
    }
}
