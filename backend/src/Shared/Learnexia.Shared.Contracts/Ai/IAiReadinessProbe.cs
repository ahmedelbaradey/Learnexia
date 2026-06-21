namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Cross-module read seam for AI-gateway readiness (P6-05 Observability).
/// Ai.Infrastructure implements; Host health check injects the interface only — never an
/// Ai-module internal type (module isolation, CONVENTIONS §12).
///
/// <para><strong>Config-inspection only.</strong> The implementation reads <c>IConfiguration</c>
/// for key presence — it makes NO paid model call and requires NO real keys in CI/dev.</para>
///
/// <para>Registered in <c>AddAiInfrastructure</c> as Scoped (mirrors
/// <see cref="IPlatformAiSafetyStatsQuery"/> and <see cref="IAiSafetyEvalResultsQuery"/>).</para>
///
/// <para><strong>Security:</strong> this interface exposes booleans/string-enum values only —
/// NEVER key values. The implementing adapter reads key presence (non-empty check) and reports
/// a boolean; the actual key characters never leave <c>Ai.Infrastructure</c>.</para>
/// </summary>
public interface IAiReadinessProbe
{
    /// <summary>
    /// Returns the current AI-gateway readiness status.
    /// Never null, never throws — any configuration-read error returns a safe default.
    /// </summary>
    AiReadinessStatus GetStatus();
}

/// <summary>
/// AI-gateway readiness status as reported by <see cref="IAiReadinessProbe"/>.
/// All fields are boolean or string-enum — no key values, no secrets.
/// </summary>
/// <param name="ProviderConfigured">
/// <c>true</c> when at least one provider API key is present in configuration
/// (<c>Ai:Providers:Claude:ApiKey</c> or <c>Ai:Providers:OpenAi:ApiKey</c> is non-empty).
/// </param>
/// <param name="DefaultProvider">
/// The configured default provider name (e.g. "Claude"). Never a key value.
/// Defaults to "Claude" when unconfigured.
/// </param>
/// <param name="RagConfigured">
/// <c>true</c> when RAG context is configured (<c>AiHelper:ContextProvider == "Rag"</c> AND
/// <c>Curriculum:Embedding:BaseUrl</c> is non-empty AND <c>Curriculum:Embedding:ModelVersion</c>
/// is non-empty). <c>false</c> means RAG is dormant (<c>EmptyLearningContextProvider</c>) —
/// this is a healthy/info state, NOT an error.
/// </param>
public record AiReadinessStatus(
    bool ProviderConfigured,
    string DefaultProvider,
    bool RagConfigured);
