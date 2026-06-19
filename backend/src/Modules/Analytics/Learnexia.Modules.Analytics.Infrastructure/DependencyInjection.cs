using Learnexia.Modules.Analytics.Infrastructure.Behaviors;
using Learnexia.Modules.Analytics.Infrastructure.Persistence;
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

        // Option-C service seams registered here as they are added in later batches (BE-3, BE-4, BE-5).
        // Placeholder: no services registered yet — entities and store arrive in BE-1/BE-3.

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
