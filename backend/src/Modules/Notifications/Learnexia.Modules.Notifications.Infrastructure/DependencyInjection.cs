using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Infrastructure.Email;
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

        AddEmailSender(services, configuration);

        return services;
    }

    /// <summary>
    /// Binds <see cref="EmailSettings"/> from the <c>Email</c> section (mirrors Identity's JwtSettings
    /// binding) and registers the single <see cref="IEmailSender"/> adapter selected by
    /// <c>Email:Provider</c>. Defaults to the no-op <see cref="LogEmailSender"/> so the stack runs with no
    /// real SMTP server (the Development default). No provider-selection Strategy/Factory — a one-time
    /// switch at composition root until a second real provider exists (P1-13a pattern gate).
    /// </summary>
    private static void AddEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(EmailSettings.SectionName).Get<EmailSettings>() ?? new EmailSettings();
        services.AddSingleton(settings);

        switch (settings.Provider)
        {
            case EmailProvider.Smtp:
                // Fail fast on a misconfigured SMTP provider rather than silently failing every send at runtime
                // (mirrors Identity's GuardJwtSecret approach). Host must be configured; credentials come from env.
                if (string.IsNullOrWhiteSpace(settings.Host))
                {
                    throw new InvalidOperationException(
                        "Email:Provider is 'Smtp' but Email:Host is not configured. Set Email__Host (and credentials via env) or use Provider 'None'.");
                }
                services.AddScoped<IEmailSender, SmtpEmailSender>();
                break;
            case EmailProvider.None:
            default:
                services.AddScoped<IEmailSender, LogEmailSender>();
                break;
        }
    }
}
