using Learnexia.Shared.Contracts.Ai;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Ai.Infrastructure.Readiness;

/// <summary>
/// Implements <see cref="IAiReadinessProbe"/> by inspecting <c>IConfiguration</c> for
/// AI-gateway key presence and RAG configuration (P6-05 Observability).
///
/// <para><strong>Config-inspection only</strong> — makes NO paid model call and requires
/// NO real keys in CI/dev environments.</para>
///
/// <para><strong>Security rule (HARD):</strong> key characters are NEVER exposed. Only the
/// boolean presence/absence is reported. This mirrors the existing <c>AiModule.InitializeAsync</c>
/// pattern which logs "key present" vs "key absent" without logging key values.</para>
///
/// <para>Registered as Scoped in <c>AddAiInfrastructure</c>, mirroring
/// <see cref="IAiSafetyEvalResultsQuery"/> and <see cref="IPlatformAiSafetyStatsQuery"/>.</para>
/// </summary>
public sealed class AiReadinessProbe : IAiReadinessProbe
{
    private readonly IConfiguration _configuration;

    public AiReadinessProbe(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <inheritdoc/>
    public AiReadinessStatus GetStatus()
    {
        // ── Provider key presence ────────────────────────────────────────────────
        // Mirror AiModule.InitializeAsync: check presence only — NEVER read or log key values.
        var claudeKey = _configuration["Ai:Providers:Claude:ApiKey"];
        var openAiKey = _configuration["Ai:Providers:OpenAi:ApiKey"];
        var providerConfigured =
            !string.IsNullOrWhiteSpace(claudeKey) ||
            !string.IsNullOrWhiteSpace(openAiKey);

        // Default provider name — matches AiGatewayOptions.DefaultProvider default.
        var defaultProvider = _configuration["Ai:Gateway:DefaultProvider"] ?? "Claude";

        // ── RAG readiness ────────────────────────────────────────────────────────
        // RagConfigured = ContextProvider is "Rag" AND BaseUrl is set AND ModelVersion is set.
        // Absent/non-Rag ContextProvider → RAG is intentionally dormant (EmptyLearningContextProvider)
        // — that is healthy/info state, NOT a failure.
        var contextProvider = _configuration["AiHelper:ContextProvider"] ?? string.Empty;
        var embeddingBaseUrl = _configuration["Curriculum:Embedding:BaseUrl"] ?? string.Empty;
        var embeddingModelVersion = _configuration["Curriculum:Embedding:ModelVersion"] ?? string.Empty;
        var ragConfigured =
            contextProvider.Equals("Rag", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(embeddingBaseUrl) &&
            !string.IsNullOrWhiteSpace(embeddingModelVersion);

        return new AiReadinessStatus(
            ProviderConfigured: providerConfigured,
            DefaultProvider: defaultProvider,
            RagConfigured: ragConfigured);
    }
}
