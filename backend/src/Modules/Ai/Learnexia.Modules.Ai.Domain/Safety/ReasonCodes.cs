namespace Learnexia.Modules.Ai.Domain.Safety;

/// <summary>
/// Stable set of reason-code string constants for safety check outcomes.
///
/// <para><strong>Stability contract:</strong> these strings are stored in
/// <c>ai.SafetyEvents.ReasonCodes</c> (jsonb array) and consumed by P7-09 (moderation queue)
/// and P7-11 (safety dashboard) for categorization. Do NOT rename or remove a code once
/// the table has rows — add new codes alongside existing ones.</para>
///
/// <para>Used as the <c>ReasonCode</c> field in both
/// <see cref="Learnexia.Shared.Contracts.Ai.CheckResult"/> (the per-call result) and
/// <see cref="CheckVerdict"/> (returned from each check implementation).</para>
/// </summary>
public static class ReasonCodes
{
    // ── Pass ──────────────────────────────────────────────────────────────────

    /// <summary>Content passed this check — no concerns detected.</summary>
    public const string Pass = "PASS";

    // ── Toxicity ──────────────────────────────────────────────────────────────

    /// <summary>Content contains high-confidence toxic language.</summary>
    public const string ToxicityHigh = "TOXICITY_HIGH";

    /// <summary>Content contains moderate toxic language (may trigger regeneration).</summary>
    public const string ToxicityModerate = "TOXICITY_MODERATE";

    /// <summary>
    /// The toxicity check failed to complete (HTTP error, timeout, unexpected exception).
    /// <strong>Fail-closed: this reason code maps to <see cref="Learnexia.Shared.Contracts.Ai.CheckOutcome.Block"/></strong>
    /// so unscreened content is never returned.
    /// </summary>
    public const string ToxicityCheckError = "TOXICITY_CHECK_ERROR";

    // ── Age Appropriateness ───────────────────────────────────────────────────

    /// <summary>Content contains adult, violent, or age-inappropriate material for school students.</summary>
    public const string AgeInappropriate = "AGE_INAPPROPRIATE";

    /// <summary>Content contains borderline age-inappropriate material (may trigger regeneration).</summary>
    public const string AgeBorderline = "AGE_BORDERLINE";

    /// <summary>
    /// The age-appropriateness check failed to complete (HTTP error, timeout, unexpected exception).
    /// <strong>Fail-closed: maps to <see cref="Learnexia.Shared.Contracts.Ai.CheckOutcome.Block"/>.</strong>
    /// </summary>
    public const string AgeCheckError = "AGE_CHECK_ERROR";

    // ── Hallucination ─────────────────────────────────────────────────────────

    /// <summary>Content likely contains an ungrounded or fabricated factual claim.</summary>
    public const string PotentialHallucination = "POTENTIAL_HALLUCINATION";

    /// <summary>Content exhibits uncertainty language cues that may indicate low-confidence output.</summary>
    public const string HallucinationUncertainty = "HALLUCINATION_UNCERTAINTY";

    /// <summary>
    /// The hallucination check failed to complete (HTTP error, timeout, unexpected exception).
    /// <strong>Fail-closed: maps to <see cref="Learnexia.Shared.Contracts.Ai.CheckOutcome.Block"/>.</strong>
    /// </summary>
    public const string HallucinationCheckError = "HALLUCINATION_CHECK_ERROR";

    // ── Input screen ──────────────────────────────────────────────────────────

    /// <summary>The student's input prompt itself was flagged as overtly unsafe (Q4 — light input screen).</summary>
    public const string UnsafeInput = "UNSAFE_INPUT";

    // ── Gateway / pipeline ────────────────────────────────────────────────────

    /// <summary>
    /// The AI gateway call failed or timed out. The safety layer blocked the response
    /// and returned the child-safe fallback.
    /// </summary>
    public const string GatewayError = "GATEWAY_ERROR";
}
