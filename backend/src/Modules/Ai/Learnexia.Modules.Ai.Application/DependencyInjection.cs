using FluentValidation;
using Learnexia.Modules.Ai.Application.Features.Explain.Commands;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.PromptBuilder.Stubs;
using Learnexia.Modules.Ai.Application.Services;
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

        // ── P3-04 Explain Feature ─────────────────────────────────────────────────────

        // FluentValidation for ExplainConceptCommand — discovered by ValidationBehavior.
        services.AddValidatorsFromAssemblyContaining<ExplainConceptCommandValidator>(ServiceLifetime.Transient);

        // RedirectResponseBuilder: localized refuse-and-redirect copy for the Explain (and future Hint) intents.
        services.AddTransient<RedirectResponseBuilder>();

        // AiTutorRateLimiter: per-student fixed-window rate limiter for the explain endpoint (BE-5).
        // Uses a ConcurrentDictionary as its counter store (in-process only — no IMemoryCache).
        // Singleton — the counter dictionary must survive across requests within the same process.
        services.AddSingleton<AiTutorRateLimiter>();

        return services;
    }
}
