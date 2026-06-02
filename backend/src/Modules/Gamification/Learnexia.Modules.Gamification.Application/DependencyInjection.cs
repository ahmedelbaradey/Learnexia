using System.Reflection;
using FluentValidation;
using Learnexia.Modules.Gamification.Application.Configuration;
using Learnexia.Shared.Kernel.Behaviors;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Gamification.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddGamificationApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = Assembly.GetExecutingAssembly();
        // MediatR is registered ONCE at the Host across ALL module Application assemblies (ADR 0002 §4)
        // so IPublisher.Publish fans out cross-module. Do NOT call AddMediatR per module.
        // Validators, AutoMapper, and the per-module ValidationBehavior stay here. The per-module
        // UnitOfWorkBehavior is registered in Infrastructure DI, AFTER this ValidationBehavior.
        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        // Streak options (P4-03-B2-8): bound from appsettings.json:Gamification:Streak.
        services.Configure<StreakOptions>(configuration.GetSection(StreakOptions.SectionName));

        // Hearts options (P4-04-B2a-2): bound from appsettings.json:Gamification:Hearts.
        services.Configure<HeartsOptions>(configuration.GetSection(HeartsOptions.SectionName));

        // Mission options (P4-06): bound from appsettings.json:Gamification:Mission.
        services.Configure<MissionOptions>(configuration.GetSection(MissionOptions.SectionName));

        // League options (P4-07): bound from appsettings.json:Gamification:League.
        services.Configure<LeagueOptions>(configuration.GetSection(LeagueOptions.SectionName));

        // Re-engagement options (P4-09): bound from appsettings.json:Gamification:Reengagement.
        // Controls cron schedules for StreakAtRiskJob + LapseWinBackJob, lapse thresholds,
        // and the mission-reminder lookahead window.
        services.Configure<ReengagementOptions>(configuration.GetSection(ReengagementOptions.SectionName));

        return services;
    }
}
