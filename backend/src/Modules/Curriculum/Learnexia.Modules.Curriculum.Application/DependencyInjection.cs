using System.Reflection;
using FluentValidation;
using Learnexia.Shared.Kernel.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Curriculum.Application;

public static class DependencyInjection
{
    /// <summary>
    /// Registers Curriculum Application-layer services.
    ///
    /// <para>Registers:
    /// <list type="bullet">
    ///   <item>AutoMapper profile scan (picks up <c>CurriculumChunkProfile</c> and any future profiles).</item>
    ///   <item>FluentValidation validator scan (for future <c>ICommand</c>-backed validators).</item>
    ///   <item><c>ValidationBehavior</c> pipeline — runs only for <c>ICommand</c> requests.</item>
    /// </list>
    /// </para>
    ///
    /// <para>Note: <c>AddMediatR</c> is NOT called here — the Host calls <c>AddCrossModuleMediatR</c>
    /// once for all modules (ADR 0002 §4). Mirrors <c>LearningModule.AddLearningApplication</c>.</para>
    /// </summary>
    public static IServiceCollection AddCurriculumApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        services.AddValidatorsFromAssembly(assembly);
        services.AddAutoMapper(cfg => cfg.AddMaps(assembly));
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

        return services;
    }
}
