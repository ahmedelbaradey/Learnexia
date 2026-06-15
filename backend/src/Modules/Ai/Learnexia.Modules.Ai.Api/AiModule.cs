using Learnexia.Modules.Ai.Application;
using Learnexia.Modules.Ai.Infrastructure;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Ai.Api;

/// <summary>
/// Module entry point for the Ai module. Called from the Host <c>Program.cs</c> via
/// <c>builder.Services.AddAiModule(builder.Configuration)</c>.
///
/// P3-01 ships NO HTTP endpoint (P3-04 owns the first AI endpoint). This module registers
/// the gateway seam (<see cref="Learnexia.Shared.Contracts.Ai.IAiGateway"/>) for all
/// other modules to inject.
/// </summary>
public static class AiModule
{
    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAiApplication(configuration);
        services.AddAiInfrastructure(configuration);
        return services;
    }

    /// <summary>
    /// Host-callable startup hook. Logs the AI provider key configuration status so the
    /// lead can confirm at a glance whether the AI Tutor is dormant or active.
    ///
    /// <para><strong>WI-C2 graceful degrade:</strong> absent provider keys → AI gateway
    /// returns <c>AiError.Unavailable</c> at runtime (fail-closed via <c>SafetyLayer</c>).
    /// This log is informational only — absent keys do NOT crash the host. Configure via env:
    /// <c>Ai__Providers__Claude__ApiKey</c> / <c>Ai__Providers__OpenAi__ApiKey</c>.</para>
    ///
    /// <para><strong>Secret rule:</strong> key values are NEVER logged — only presence or
    /// absence is logged.</para>
    /// </summary>
    public static Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var logger        = serviceProvider.GetRequiredService<ILoggerManager>();
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        var claudeKey  = configuration["Ai:Providers:Claude:ApiKey"];
        var openAiKey  = configuration["Ai:Providers:OpenAi:ApiKey"];
        var contextKey = configuration["AiHelper:ContextProvider"] ?? string.Empty;

        // Log provider key status — never log the key values themselves.
        if (string.IsNullOrWhiteSpace(claudeKey))
            logger.LogWarn(
                "WI-C2 AiModule [DORMANT]: Ai:Providers:Claude:ApiKey is not configured. " +
                "Claude AI calls will return AiError.Unavailable. " +
                "Set via env Ai__Providers__Claude__ApiKey to activate.");
        else
            logger.LogInfo("WI-C2 AiModule: Ai:Providers:Claude:ApiKey is present.");

        if (string.IsNullOrWhiteSpace(openAiKey))
            logger.LogWarn(
                "WI-C2 AiModule [DORMANT]: Ai:Providers:OpenAi:ApiKey is not configured. " +
                "OpenAI AI calls will return AiError.Unavailable. " +
                "Set via env Ai__Providers__OpenAi__ApiKey to activate.");
        else
            logger.LogInfo("WI-C2 AiModule: Ai:Providers:OpenAi:ApiKey is present.");

        // Log context-provider selection so it is visible at startup.
        if (contextKey.Equals("Rag", StringComparison.OrdinalIgnoreCase))
            logger.LogInfo(
                "WI-C2 AiModule: AiHelper:ContextProvider=Rag — RagContextProvider is active " +
                "(live pgvector RAG retrieval). Ensure BGE-M3 TEI is provisioned and re-embed has been run.");
        else
            logger.LogInfo(
                $"WI-C2 AiModule: AiHelper:ContextProvider='{contextKey}' (absent or non-Rag) — " +
                "EmptyLearningContextProvider is active (always redirects). " +
                "Set AiHelper:ContextProvider=Rag after provisioning TEI + re-embed to activate live grounding.");

        return Task.CompletedTask;
    }
}
