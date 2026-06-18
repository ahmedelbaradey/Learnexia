using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Infrastructure.Behaviors;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Modules.Moderation.Infrastructure.Service;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Moderation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddModerationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext — Npgsql/PostgreSQL, schema = "moderation", migrations history in that schema.
        services.AddDbContext<ModerationDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("Default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", ModerationDbContext.Schema)
                           .MigrationsAssembly(typeof(ModerationDbContext).Assembly.FullName)));

        // Option-C service seams: all EF/persistence logic lives behind these interfaces.
        // Registered Scoped so they share the per-request ModerationDbContext instance.
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.AddScoped<IAuditLogWriter, AuditLogWriter>();

        // P7-09 Moderation Queue — Option-C service seams.
        services.AddScoped<IModerationQueueWriter, ModerationQueueWriter>();
        services.AddScoped<IModerationQueryService, ModerationQueryService>();
        services.AddScoped<IModerationItemService, ModerationItemService>();

        services.AddHttpContextAccessor();
        services.AddSingleton<ILoggerManager, LoggerManager>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // Unit-of-Work behavior (ADR 0001 §2 + ADR 0002 §2): commit once per ICommand<>, then dispatch
        // domain events AFTER commit. Scaffolded now so P7-09 write commands work without revisiting
        // shared-file serialization points. P7-12 itself has no write commands; this fires on zero.
        // Registered here in Infrastructure (not Application) because it injects ModerationDbContext.
        // Registered AFTER ValidationBehavior (added in AddModerationApplication, called before this).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }
}
