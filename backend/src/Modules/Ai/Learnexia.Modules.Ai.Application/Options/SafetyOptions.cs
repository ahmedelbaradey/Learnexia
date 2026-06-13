namespace Learnexia.Modules.Ai.Application.Options;

/// <summary>
/// Safety Layer configuration, bound from <c>Ai:Safety</c> in appsettings / secret config.
///
/// <para><strong>FR-AI-4 defaults:</strong> all three check flags default to <c>true</c>.
/// Disabling any check requires an explicit configuration change — there is no easy-to-hit
/// bypass path. These settings are intentionally not exposed in <c>SafetyOptions</c>
/// themselves as flags the application reads at startup; the operator must change config
/// deliberately.</para>
///
/// <para><strong>API key rule:</strong> moderation provider API keys are NOT stored in this class.
/// They live in secret configuration under <c>Ai:Providers:&lt;Provider&gt;:ApiKey</c>
/// (env vars / user secrets / vault). This class is safe to log — it contains no secrets.</para>
/// </summary>
public sealed class SafetyOptions
{
    /// <summary>Configuration section name for binding.</summary>
    public const string SectionName = "Ai:Safety";

    // ── Per-check enable flags (FR-AI-4: all true by default) ─────────────────

    /// <summary>
    /// Enable the toxicity check. Default: <c>true</c>.
    /// Must be set to <c>false</c> explicitly to disable; changing in prod is a compliance risk.
    /// </summary>
    public bool EnableToxicityCheck { get; set; } = true;

    /// <summary>
    /// Enable the age-appropriateness check. Default: <c>true</c>.
    /// Must be set to <c>false</c> explicitly to disable; changing in prod is a compliance risk.
    /// </summary>
    public bool EnableAgeCheck { get; set; } = true;

    /// <summary>
    /// Enable the hallucination check. Default: <c>true</c>.
    /// Must be set to <c>false</c> explicitly to disable; changing in prod is a compliance risk.
    /// </summary>
    public bool EnableHallucinationCheck { get; set; } = true;

    // ── Regeneration control ───────────────────────────────────────────────────

    /// <summary>
    /// Maximum number of times the safety layer will re-prompt the gateway when a check
    /// returns <see cref="Learnexia.Shared.Contracts.Ai.CheckOutcome.NeedsRegeneration"/>
    /// (and no check returned <see cref="Learnexia.Shared.Contracts.Ai.CheckOutcome.Block"/>).
    ///
    /// <para>Must be ≥ 0. 0 = no regeneration; block immediately on NeedsRegeneration.
    /// Default: 2. Finite bound prevents unbounded cost/latency loops (security-auditor gate).</para>
    /// </summary>
    public int MaxRegenerationAttempts { get; set; } = 2;

    // ── Moderation provider ────────────────────────────────────────────────────

    /// <summary>
    /// The moderation provider name for toxicity and age-appropriateness checks.
    /// Used by check implementations to select the HTTP client / endpoint.
    /// E.g. <c>"Claude"</c> (LLM-as-judge via <c>IAiGateway</c>) or <c>"OpenAI"</c>.
    ///
    /// <para>Default: <c>"Claude"</c> (cheap-tier LLM-as-judge via <c>IAiGateway</c>
    /// — no separate moderation endpoint needed at build time).</para>
    /// </summary>
    public string ModerationProvider { get; set; } = "Claude";

    // ── Fallback copy localization keys ───────────────────────────────────────

    /// <summary>
    /// <c>SharedResourcesKey</c> constant for the child-safe fallback message returned
    /// when AI content is blocked. Resolved through the string localizer
    /// by <c>SafetyLayer</c>. Default: <c>"AiContentBlocked"</c>.
    /// </summary>
    public string FallbackMessageKey { get; set; } = "AiContentBlocked";

    /// <summary>
    /// <c>SharedResourcesKey</c> constant for the child-safe fallback message returned
    /// when the AI gateway itself failed (unavailable / error). Default: <c>"AiServiceUnavailable"</c>.
    /// </summary>
    public string GatewayErrorFallbackKey { get; set; } = "AiServiceUnavailable";
}
