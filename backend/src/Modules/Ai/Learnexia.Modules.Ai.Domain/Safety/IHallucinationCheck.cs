namespace Learnexia.Modules.Ai.Domain.Safety;

/// <summary>
/// Checks AI-generated content for likely hallucinations — ungrounded factual claims,
/// uncertainty language, or content inconsistent with the supplied RAG context.
///
/// <para><strong>Heuristic-first for P3-02:</strong> until the RAG pipeline (P3-07) is available
/// the check uses linguistic heuristics (uncertainty cues, ungrounded claim markers).
/// When <paramref name="ragContext"/> is supplied (P3-07+) the implementation compares
/// the AI output against the provided curriculum chunks for factual grounding.</para>
///
/// <para><strong>Fail-closed contract:</strong> on any error, timeout, or unexpected exception
/// the implementation MUST return <see cref="CheckVerdict.Block"/> with reason code
/// <see cref="ReasonCodes.HallucinationCheckError"/>. Returning <see cref="CheckVerdict.Pass"/>
/// when the check cannot complete is a security violation (FR-AI-4, AC3).</para>
///
/// <para>Implemented by <c>HallucinationCheck</c> in <c>Ai.Infrastructure/Safety/</c>.</para>
/// </summary>
public interface IHallucinationCheck
{
    /// <summary>
    /// Evaluate <paramref name="content"/> for likely hallucination in the given <paramref name="language"/>.
    /// </summary>
    /// <param name="content">The AI-generated text to evaluate. Never null.</param>
    /// <param name="language">Content language code, e.g. <c>"ar"</c> or <c>"en"</c>.</param>
    /// <param name="ct">Cancellation token. On cancellation, return <see cref="CheckVerdict.Block"/> (fail-closed).</param>
    /// <returns>A <see cref="CheckVerdict"/> — never throws.</returns>
    Task<CheckVerdict> CheckAsync(string content, string language, CancellationToken ct);

    /// <summary>
    /// Evaluate <paramref name="content"/> against the supplied <paramref name="ragContext"/> chunks
    /// (P3-07 RAG grounding).
    ///
    /// <para>When <paramref name="ragContext"/> is null or empty, falls back to heuristic detection only
    /// (same as the single-overload <see cref="CheckAsync(string, string, CancellationToken)"/>).</para>
    /// </summary>
    /// <param name="content">The AI-generated text to evaluate. Never null.</param>
    /// <param name="language">Content language code, e.g. <c>"ar"</c> or <c>"en"</c>.</param>
    /// <param name="ragContext">Optional curriculum chunks for grounded hallucination scoring. Null until P3-07.</param>
    /// <param name="ct">Cancellation token. On cancellation, return <see cref="CheckVerdict.Block"/> (fail-closed).</param>
    /// <returns>A <see cref="CheckVerdict"/> — never throws.</returns>
    Task<CheckVerdict> CheckAsync(
        string content,
        string language,
        IReadOnlyList<CurriculumChunk>? ragContext,
        CancellationToken ct);
}
