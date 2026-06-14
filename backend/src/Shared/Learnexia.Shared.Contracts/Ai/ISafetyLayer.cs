namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The mandatory AI-content safety facade.
///
/// <para><strong>AC1 (P3-02) — no-bypass invariant:</strong>
/// <see cref="GenerateSafeAsync"/> is the ONLY path through which AI-generated content
/// reaches a feature handler. It internally calls <see cref="IAiGateway"/> and screens
/// the raw output through all enabled checks before returning. Feature handlers never
/// call <see cref="IAiGateway"/> directly — the architecture test (P3-02-BE-10) enforces
/// this at build time.</para>
///
/// <para><strong>FR-AI-4 (mandatory):</strong> all three checks (toxicity, age-appropriateness,
/// hallucination) are enabled by default. Disabling any check requires an explicit config change
/// — there is no easy-to-hit bypass path.</para>
///
/// <para><strong>Fail-closed guarantee:</strong> on any error — gateway failure, check exception,
/// cancellation — the layer blocks the content and returns the child-safe localized fallback.
/// Unscreened content is NEVER returned to the caller.</para>
///
/// <para><strong>R5 coupling (runtime-review gate):</strong> the returned <see cref="SafeAiResult"/>
/// carries <c>Confidence</c> forwarded from the raw gateway result. Feature handlers
/// (P3-04/05/06) use it — with the <c>SafetyVerdict</c> — to decide <c>ReviewStatus</c>
/// when writing to <c>ai.AiResponseCache</c>. The Safety Layer is the gatekeeper;
/// the cache write is owned by the feature handler.</para>
///
/// Implemented by <c>SafetyLayer</c> in <c>Ai.Application/Safety/</c>.
/// </summary>
public interface ISafetyLayer
{
    /// <summary>
    /// Generate a safety-screened AI completion for <paramref name="request"/>.
    ///
    /// <para>The method:</para>
    /// <list type="number">
    ///   <item>Optionally screens the <paramref name="request"/> input for overtly unsafe student content.</item>
    ///   <item>Calls <see cref="IAiGateway.CompleteAsync"/> to obtain a raw completion.</item>
    ///   <item>Runs all enabled checks concurrently (<see cref="IToxicityCheck"/>,
    ///         <see cref="IAgeAppropriatenessCheck"/>, <see cref="IHallucinationCheck"/>).</item>
    ///   <item>Blocks on any <see cref="CheckOutcome.Block"/>; regenerates (bounded) on
    ///         <see cref="CheckOutcome.NeedsRegeneration"/>.</item>
    ///   <item>On block / max-regen exhausted: writes a <c>SafetyEvent</c> and returns
    ///         the child-safe localized fallback — <strong>never</strong> the unsafe content.</item>
    ///   <item>On pass: returns the screened content as <see cref="SafeAiResult.Allowed"/> = true.</item>
    ///   <item><strong>Fail-closed on any error</strong>: exception / timeout / cancellation →
    ///         block + fallback. Unscreened content is never returned.</item>
    /// </list>
    ///
    /// <para>Never throws — all failures are encoded in the returned <see cref="SafeAiResult"/>
    /// with <see cref="SafetyVerdict.Blocked"/> or <see cref="SafetyVerdict.Fallback"/>.</para>
    /// </summary>
    Task<SafeAiResult> GenerateSafeAsync(AiRequest request, CancellationToken ct = default);
}
