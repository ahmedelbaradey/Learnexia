using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Application.Options;
using Learnexia.Modules.Analytics.Infrastructure.Behaviors;
using Learnexia.Modules.Analytics.Infrastructure.Contracts;
using Learnexia.Modules.Analytics.Infrastructure.Persistence;
using Learnexia.Modules.Analytics.Infrastructure.Services;
using Learnexia.Shared.Contracts.Analytics;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Analytics.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAnalyticsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext — Npgsql/PostgreSQL, schema = "analytics", migrations history in that schema.
        services.AddDbContext<AnalyticsDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("Default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", AnalyticsDbContext.Schema)
                           .MigrationsAssembly(typeof(AnalyticsDbContext).Assembly.FullName)));

        // BE-3: append-only activity-event store (Option C — EF only in Infrastructure).
        // Scoped: shares the AnalyticsDbContext scope (one DbContext per request / consumer invocation).
        // Mirrors AiUsageLogStore registration in Ai.Infrastructure.
        services.AddScoped<IActivityEventStore, ActivityEventStore>();

        // BE-4: session/retention derivation service. Scoped: shares the AnalyticsDbContext scope.
        // Reads analytics.ActivityEvents AsNoTracking, derives sessions and retention in-memory.
        services.AddScoped<IActivitySessionService, ActivitySessionService>();

        // P9-11: notification lifecycle aggregate service. Scoped: shares the AnalyticsDbContext scope.
        // Groups analytics.ActivityEvents by notification EventTypes (Dispatched/Suppressed/Opened)
        // to compute send/suppress/open rates for the admin dashboard.
        services.AddScoped<INotificationAnalyticsService, NotificationAnalyticsService>();

        // BE-5: platform read seam adapter. Scoped: delegates to IActivitySessionService +
        // INotificationAnalyticsService. Registered as IPlatformAnalyticsQuery (Shared.Contracts)
        // so Identity façade can inject it without any direct reference to Analytics module projects.
        services.AddScoped<IPlatformAnalyticsQuery, PlatformAnalyticsQueryAdapter>();

        // BE-4: options — SessionGapMinutes (default 30) bound from "Analytics" section.
        services.Configure<AnalyticsOptions>(
            configuration.GetSection(AnalyticsOptions.SectionName));

        services.AddHttpContextAccessor();
        services.AddSingleton<ILoggerManager, LoggerManager>();

        // Unit-of-Work behavior (ADR 0001 §2 + ADR 0002 §2): commit once per ICommand<>, then dispatch
        // domain events AFTER commit. Scaffolded now so future write commands work without revisiting
        // shared-file serialization points. Append-only ingest (BE-3) calls SaveChangesAsync directly,
        // not through this UoW. Registered AFTER ValidationBehavior (added in AddAnalyticsApplication).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }
}
