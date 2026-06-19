using FluentValidation;
using Learnexia.Modules.Ai.Application.Features.Explain.Commands;
using Learnexia.Modules.Ai.Application.Features.Hint.Commands;
using Learnexia.Modules.Ai.Application.Features.Recommendation.Commands;
using Learnexia.Modules.Ai.Application.Features.Simplify.Commands;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.PromptBuilder;
using Learnexia.Modules.Ai.Application.PromptBuilder.Stubs;
using Learnexia.Modules.Ai.Application.Safety;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Contracts.Gamification;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Learnexia.Modules.Ai.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAiApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ── P3-03 Prompt Builder (BE-8) ──────────────────────────────────────────

        // TemplateSelector: Singleton — pure, stateless Dictionary lookup. Rule 8 satisfied
        // (plain lookup, NOT a Strategy/Factory pattern).
        services.AddSingleton<TemplateSelector>();

        // IPromptBuilder: Transient — pure/stateless assembly; no shared state.
        services.AddTransient<IPromptBuilder, PromptBuilder.PromptBuilder>();

        // IStudentWeakAreasQuery: EmptyWeakAreasQuery is the FALLBACK (returns empty).
        // TryAdd so the real implementation wins when the Learning module is loaded — Learning's
        // AiWeakAreasQueryBridge (registered earlier, in AddLearningInfrastructure, which the Host
        // runs before the Ai module) stays in place; this only registers the stub when nothing
        // else has (e.g. Ai-isolated tests).
        services.TryAddTransient<IStudentWeakAreasQuery, EmptyWeakAreasQuery>();

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

        // IStudentXpQuery: default stub returns null (no XP profile / level 1 cold-start fallback).
        // P3-14a: the Gamification module registers the real CachedStudentXpQuery via
        // AddGamificationInfrastructure. TryAddScoped so the real implementation wins in the full host.
        // The Ai module uses this seam to fetch CurrentLevel for motivational framing only —
        // the handler defaults to CurrentLevel=1 on null (seam contract).
        services.TryAddScoped<IStudentXpQuery, DefaultStudentXpQuery>();

        // ── P3-04 Explain Feature ─────────────────────────────────────────────────────

        // FluentValidation for ExplainConceptCommand — discovered by ValidationBehavior.
        services.AddValidatorsFromAssemblyContaining<ExplainConceptCommandValidator>(ServiceLifetime.Transient);

        // RedirectResponseBuilder: localized refuse-and-redirect copy for the Explain (and future Hint) intents.
        services.AddTransient<RedirectResponseBuilder>();

        // IAiTutorRateLimiter: registered as in-process fallback here in Application (TryAdd so
        // the Infrastructure-registered RedisAiRateLimiter takes precedence when Redis is available).
        // The concrete AiTutorRateLimiter is also kept as a direct Singleton so existing tests
        // that inject it by concrete type continue to resolve (the interface is what handlers use).
        services.TryAddSingleton<IAiTutorRateLimiter, AiTutorRateLimiter>();
        services.AddSingleton<AiTutorRateLimiter>();

        // ── P10-03 CreditCostResolver — server-side intent→cost mapping ──────────────
        // Singleton: reads IGlobalSettingsProvider (Singleton) + IConfiguration (Singleton).
        // ICreditSpendService implementation is registered by AddBillingInfrastructure in the
        // Billing module — no Billing.* project reference needed here (module isolation rule 1).
        services.AddSingleton<CreditCostResolver>();

        // ── P3-05 Hint + WhyWrong + Simplify Features ────────────────────────────────

        // HintOptions: config-bound POCO for MaxHintLevels (OQ-5).
        // Bound from "Ai:Hint" section; default MaxHintLevels = 3.
        services.Configure<HintOptions>(configuration.GetSection(HintOptions.SectionName));

        // FluentValidation for GetHintCommand and SimplifyExplanationCommand.
        // AddValidatorsFromAssemblyContaining is idempotent — scanning the same assembly twice is safe.
        services.AddValidatorsFromAssemblyContaining<GetHintCommandValidator>(ServiceLifetime.Transient);
        services.AddValidatorsFromAssemblyContaining<SimplifyExplanationCommandValidator>(ServiceLifetime.Transient);

        // ── P3-14 Recommendation narration ───────────────────────────────────────────────
        // FluentValidation for RecommendationNarrationCommand (empty-body command; validator required
        // for ValidationBehavior discovery and future extension).
        services.AddValidatorsFromAssemblyContaining<RecommendationNarrationCommandValidator>(ServiceLifetime.Transient);

        // ── P7-09 Moderation Queue ingest — AI-side publish seam ─────────────────────────
        // Registered Transient (scoped to the request that calls SafetyLayer.GenerateSafeAsync).
        // Wraps MediatR IPublisher; fail-soft (never throws into the safety path).
        services.AddTransient<IAiOutputFlaggedPublisher, AiOutputFlaggedPublisher>();

        return services;
    }
}
