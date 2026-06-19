using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Infrastructure.Email;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Push;
using Learnexia.Modules.Notifications.Infrastructure.Reengagement;
using Learnexia.Modules.Notifications.Infrastructure.Services;
using Learnexia.Shared.Kernel.Settings;
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

        AddEmailSender(services, configuration);
        AddPushSender(services, configuration);

        // NudgeDispatcher composes IPushSender + IDeviceTokenService + INotificationInboxService + ISystemClock.
        // Scoped — matches the lifetime of NotificationsDbContext.
        services.AddScoped<INudgeDispatcher, NudgeDispatcher>();

        // Option-C service layer: EF-free Application handlers inject only these interfaces.
        services.AddScoped<INotificationInboxService, NotificationInboxService>();
        services.AddScoped<INotificationPreferenceService, NotificationPreferenceService>();
        services.AddScoped<IChildReengagementPreferenceService, ChildReengagementPreferenceService>();
        services.AddScoped<IDeviceTokenService, DeviceTokenService>();
        services.AddScoped<IReengagementDedupeStore, ReengagementDedupeStore>();

        // P9-07: cross-category push-budget arbiter (Scoped — same lifetime as NudgeDispatcher).
        services.AddScoped<INudgeArbiter, NudgeArbiter>();

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

    /// <summary>
    /// Binds <see cref="PushSettings"/> from <c>Notifications:Push</c> and registers the
    /// <see cref="IPushSender"/> adapter selected by <c>Provider</c> (enum-switch, no Strategy/Factory
    /// per rule 8 — mirrors <c>AddEmailSender</c> pattern). Defaults to <see cref="LogPushSender"/>
    /// so the stack runs without any push infrastructure in dev.
    /// </summary>
    private static void AddPushSender(IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(PushSettings.SectionName).Get<PushSettings>() ?? new PushSettings();
        services.AddOptions<PushSettings>().Bind(configuration.GetSection(PushSettings.SectionName));

        switch (settings.Provider)
        {
            case PushProvider.Expo:
                services.AddHttpClient<ExpoPushSender>(c =>
                {
                    c.BaseAddress = new Uri(settings.Expo.BaseUrl);
                    // F-07: bound worst-case dispatch latency — prevent thread-pool starvation
                    // if the Expo API is slow (default HttpClient timeout is 100 seconds).
                    c.Timeout = TimeSpan.FromSeconds(10);
                });
                services.AddScoped<IPushSender, ExpoPushSender>();
                break;
            case PushProvider.None:
            default:
                services.AddScoped<IPushSender, LogPushSender>();
                break;
        }
    }
}
