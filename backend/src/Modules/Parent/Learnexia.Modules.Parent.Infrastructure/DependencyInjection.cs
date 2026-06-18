using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Modules.Parent.Infrastructure.Behaviors;
using Learnexia.Modules.Parent.Infrastructure.Jobs;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Modules.Parent.Infrastructure.Service;
using Learnexia.Shared.Contracts.Parent;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Parent.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddParentInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext(configuration);
        services.AddHttpContextAccessor();

        // ILoggerManager is registered globally as a Singleton at the Host (Identity.Infrastructure
        // AddLoggerServices). Do NOT register it again here — a duplicate Singleton registration causes
        // dual log pipelines and violates CLAUDE.md rule 5. Rely on the global singleton instead.
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<ILinkParentStudentService, LinkParentStudentService>();

        // Parent-side implementation of the IParentChildQuery seam (Shared.Contracts): lets Identity's Me
        // endpoint read HasChildren without referencing the Parent module. Real implementation, not a stub.
        services.AddScoped<IParentChildQuery, ParentChildQuery>();

        // P5-01-BE-2: weekly report generator service (upsert, Infrastructure owns EF writes).
        services.AddScoped<IWeeklyReportGeneratorService, WeeklyReportGeneratorService>();

        // P5-01-BE-4: EF-free read service for the E9 weekly report read endpoint (Option-C).
        services.AddScoped<IWeeklyReportQueryService, WeeklyReportQueryService>();

        // P5-01-BE-3: Hangfire job — registered as transient so Hangfire can resolve it.
        services.AddTransient<WeeklyReportJob>();

        // Unit-of-Work behavior (ADR 0001 §2 + ADR 0002 §2): commit once per ICommand<>, then dispatch
        // domain events AFTER commit. Registered here in Infrastructure (injects the concrete
        // ParentDbContext) and AFTER ValidationBehavior (added in AddParentApplication, called first).
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }

    public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ParentDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", ParentDbContext.Schema)
                           .MigrationsAssembly(typeof(ParentDbContext).Assembly.FullName)));
    }
}
