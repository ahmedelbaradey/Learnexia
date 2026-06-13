namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The ONLY AI-content type that feature handlers may consume.
/// Produced exclusively by <see cref="ISafetyLayer.GenerateSafeAsync"/>.
///
/// <para>AC1 (P3-02) — no feature handler ever receives a raw <see cref="AiResult"/>
/// from <see cref="IAiGateway"/> directly. Every piece of AI content has been screened
/// before it reaches a student.</para>
///
/// <para>R5 coupling (P3-02 brief, runtime-review gate): <see cref="Confidence"/> carries
/// the model-reported confidence from the underlying <see cref="AiResult"/>. Feature handlers
/// (P3-04/05/06) use this — combined with the <see cref="SafetyVerdict"/> — to decide whether
/// to auto-approve a cached response (<c>ReviewStatus = Approved</c>) or flag it as
/// <c>PendingReview</c> in <c>ai.AiResponseCache</c>. The Safety Layer is the gatekeeper
/// (pass/block/regenerate); the cache write is owned by the calling handler.</para>
/// </summary>
/// <param name="Allowed">True when <see cref="Verdict"/> is <see cref="SafetyVerdict.Allowed"/>.</param>
/// <param name="Content">
/// The content to surface to the student. When <see cref="Allowed"/> is true this is the
/// screened AI output; when false this is the child-safe localized fallback string.
/// Never null when the layer completes successfully.
/// </param>
/// <param name="Verdict">Overall safety verdict.</param>
/// <param name="Results">
/// Per-check results (empty list when the gateway itself failed before checks ran).
/// Preserved for analytics and <c>ai.SafetyEvents</c> logging.
/// </param>
/// <param name="Confidence">
/// Optional model confidence score forwarded from the raw gateway result.
/// Feature handlers compare this against <c>AutoApprovalConfidenceThreshold</c> (P3-04)
/// to decide <c>ReviewStatus</c>. Null when the gateway did not provide a score.
/// </param>
public sealed record SafeAiResult(
    bool Allowed,
    string? Content,
    SafetyVerdict Verdict,
    IReadOnlyList<CheckResult> Results,
    decimal? Confidence = null);
