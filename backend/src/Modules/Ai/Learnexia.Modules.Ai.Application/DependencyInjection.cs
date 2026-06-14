using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.PromptBuilder.Stubs;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Learnexia.Modules.Ai.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAiApplication(this IServiceCollection services)
    {
        // ── P3-03 Prompt Builder (BE-8) ──────────────────────────────────────────

        // TemplateSelector: Singleton — pure, stateless Dictionary lookup. Rule 8 satisfied
        // (plain lookup, NOT a Strategy/Factory pattern).
        services.AddSingleton<TemplateSelector>();

        // IPromptBuilder: Transient — pure/stateless assembly; no shared state.
        services.AddTransient<IPromptBuilder, PromptBuilder.PromptBuilder>();

        // IStudentWeakAreasQuery: default stub returns empty list.
        // P3-09 overrides this registration with the real implementation.
        services.AddTransient<IStudentWeakAreasQuery, EmptyWeakAreasQuery>();

        // ICurriculumContextQuery: default stub returns empty list.
        // P3-07 overrides this registration with the real RAG-backed implementation.
        services.AddTransient<ICurriculumContextQuery, EmptyCurriculumContextQuery>();

        // ILearningContextProvider: default stub returns empty chunks.
        // BE-10 (SeededCorpusContextProvider) overrides this; later P3-07 (RagContextProvider) swaps.
        // Uses TryAdd so the real implementation registered by BE-10 or P3-07 takes precedence.
        services.TryAddTransient<ILearningContextProvider, EmptyLearningContextProvider>();

        // IChildLearningProfileQuery: default stub returns safe defaults (Grade 4, Age 10, Ar).
        // P3-04 wires the real implementation from the Identity/Parent seam.
        // Uses TryAdd so a real implementation registered first is not overridden.
        services.TryAddTransient<IChildLearningProfileQuery, DefaultChildLearningProfileQuery>();

        return services;
    }
}
