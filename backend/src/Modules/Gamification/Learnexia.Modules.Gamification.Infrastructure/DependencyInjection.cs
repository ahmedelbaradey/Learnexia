using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Infrastructure.Behaviors;
using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Gamification.Infrastructure.Queries;
using Learnexia.Modules.Gamification.Infrastructure.Repository;
using Learnexia.Modules.Gamification.Infrastructure.Service;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Gamification.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddGamificationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext(configuration);

        services.AddSingleton<ILoggerManager, LoggerManager>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<IGamificationRepository, GamificationRepository>();

        // Cross-module read seam: Learning dashboard injects IStudentXpQuery to read XP without
        // referencing GamificationDbContext directly (module isolation rule 1). Mirrors IParentChildQuery.
        // Future P4-10 swaps this for a Redis-backed implementation behind the same seam.
        services.AddScoped<IStudentXpQuery, StudentXpQuery>();

        // Unit-of-Work behavior (ADR 0001 §2 + ADR 0002 §2): commit once per ICommand<>, then dispatch
        // domain events AFTER commit. Registered here in Infrastructure (not Application) because it
        // injects the concrete GamificationDbContext. Registered AFTER ValidationBehavior (added in
        // AddGamificationApplication, which is called before this) so validation rejects bad input first.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }

    public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<GamificationDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", GamificationDbContext.Schema)
                           .MigrationsAssembly(typeof(GamificationDbContext).Assembly.FullName)));
    }
}
