using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Infrastructure.Behaviors;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Repository;
using Learnexia.Modules.Learning.Infrastructure.Service;
using Learnexia.Shared.Kernel.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Learning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLearningInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext(configuration);
        services.AddHttpContextAccessor();

        services.AddSingleton<ILoggerManager, LoggerManager>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<ILearningRepository, LearningRepository>();
        services.AddScoped<ILearningRepositoryManager, LearningRepositoryManager>();
        services.AddScoped<ILearningServiceManager, LearningServiceManager>();

        // Unit-of-Work behavior (ADR 0001 §2 + ADR 0002 §2): commit once per ICommand<>, then dispatch
        // domain events AFTER commit. Registered here in Infrastructure (not Application) because it
        // injects the concrete LearningDbContext. Registered AFTER ValidationBehavior (added in
        // AddLearningApplication, which is called before this) so validation rejects bad input first.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }

    public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LearningDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", LearningDbContext.Schema)
                           .MigrationsAssembly(typeof(LearningDbContext).Assembly.FullName)));
    }
}
