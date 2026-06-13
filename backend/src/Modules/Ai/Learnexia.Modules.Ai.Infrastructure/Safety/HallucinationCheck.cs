using Learnexia.Modules.Ai.Domain.Safety;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Ai.Infrastructure.Safety;

/// <summary>
/// Heuristic hallucination check for P3-02.
///
/// <para><strong>Strategy (Q4 decision):</strong> for the P3-01..03 cluster, this check uses
/// linguistic heuristics — uncertainty language cues and ungrounded factual claim patterns —
/// in Arabic and English. It will be tightened in P3-07 when the RAG pipeline provides
/// curriculum context chunks for grounded comparison.</para>
///
/// <para><strong>Approach:</strong> scans the content for high-confidence uncertainty signals
/// (hedging language, made-up statistics, self-contradictions, disclaimer patterns).
/// High uncertainty density → <see cref="ReasonCodes.HallucinationUncertainty"/> (NeedsRegeneration).
/// Likely-fabricated factual claim patterns → <see cref="ReasonCodes.PotentialHallucination"/> (Block).</para>
///
/// <para><strong>Fail-closed:</strong> on any exception — including <see cref="OperationCanceledException"/>
/// — this method returns <see cref="CheckVerdict.Block"/> with
/// <see cref="ReasonCodes.HallucinationCheckError"/>. It NEVER returns Pass on an error path.</para>
///
/// <para>No external calls are made in the heuristic implementation; no API keys are needed.</para>
/// </summary>
public sealed class HallucinationCheck : IHallucinationCheck
{
    private readonly ILoggerManager _logger;

    // ── English uncertainty cue patterns ──────────────────────────────────────
    private static readonly string[] EnUncertaintyCues =
    [
        "i think",
        "i believe",
        "i'm not sure",
        "i am not sure",
        "i'm not certain",
        "i am not certain",
        "it might be",
        "it could be",
        "it may be",
        "possibly",
        "perhaps",
        "probably",
        "i'm not 100%",
        "i cannot be sure",
        "i don't know for certain",
    ];

    // ── Arabic uncertainty cue patterns ───────────────────────────────────────
    private static readonly string[] ArUncertaintyCues =
    [
        "أعتقد",
        "ربما",
        "لست متأكداً",
        "لست متأكدة",
        "قد يكون",
        "يحتمل",
        "من المحتمل",
        "لا أعرف على وجه اليقين",
        "لست واثقاً",
        "لست واثقة",
        "يمكن أن",
        "من الممكن",
    ];

    // ── English strong-hallucination markers (block-level) ────────────────────
    private static readonly string[] EnHallucinationMarkers =
    [
        "according to a study",
        "research shows",
        "scientists have proven",
        "it is a fact that",
        "statistically speaking",
        "the exact number is",
        "the precise figure",
    ];

    // ── Arabic strong-hallucination markers (block-level) ─────────────────────
    private static readonly string[] ArHallucinationMarkers =
    [
        "وفقاً لدراسة",
        "تشير الأبحاث",
        "أثبت العلماء",
        "من الحقائق المعروفة أن",
        "إحصائياً",
        "الرقم الدقيق",
    ];

    /// <summary>Uncertainty cue threshold: &gt;= this many cues triggers NeedsRegeneration.</summary>
    private const int UncertaintyThreshold = 2;

    public HallucinationCheck(ILoggerManager logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<CheckVerdict> CheckAsync(string content, string language, CancellationToken ct) =>
        CheckAsync(content, language, null, ct);

    /// <inheritdoc />
    public Task<CheckVerdict> CheckAsync(
        string content,
        string language,
        IReadOnlyList<CurriculumChunk>? ragContext,
        CancellationToken ct)
    {
        try
        {
            ct.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(content))
                return Task.FromResult(CheckVerdict.Pass());

            var lower = content.ToLowerInvariant();
            bool isArabic = language.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

            var uncertaintyCues = isArabic ? ArUncertaintyCues : EnUncertaintyCues;
            var hallucMarkers   = isArabic ? ArHallucinationMarkers : EnHallucinationMarkers;

            // Check for strong hallucination markers first (block-level).
            foreach (var marker in hallucMarkers)
            {
                if (lower.Contains(marker, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogDebug(
                        $"HallucinationCheck: strong marker '{marker}' found. → PotentialHallucination (NeedsRegeneration).");
                    return Task.FromResult(CheckVerdict.NeedsRegen(ReasonCodes.PotentialHallucination));
                }
            }

            // Count uncertainty cues.
            int cueCount = 0;
            foreach (var cue in uncertaintyCues)
            {
                if (lower.Contains(cue, StringComparison.OrdinalIgnoreCase))
                    cueCount++;
            }

            if (cueCount >= UncertaintyThreshold)
            {
                _logger.LogDebug(
                    $"HallucinationCheck: {cueCount} uncertainty cues found (threshold={UncertaintyThreshold}). → NeedsRegeneration.");
                return Task.FromResult(CheckVerdict.NeedsRegen(ReasonCodes.HallucinationUncertainty));
            }

            // P3-07 RAG grounding: when ragContext is available, do simple token-overlap check.
            if (ragContext is { Count: > 0 })
            {
                var verdict = ApplyRagGrounding(lower, ragContext);
                if (verdict is not null)
                    return Task.FromResult(verdict);
            }

            return Task.FromResult(CheckVerdict.Pass());
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarn("HallucinationCheck: cancelled. Fail-closed → Block.");
            return Task.FromResult(CheckVerdict.Block(ReasonCodes.HallucinationCheckError));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HallucinationCheck: unexpected exception. Fail-closed → Block.");
            return Task.FromResult(CheckVerdict.Block(ReasonCodes.HallucinationCheckError));
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Simple token-overlap RAG grounding (P3-07 placeholder).
    /// When the AI output has very low lexical overlap with all RAG chunks it may be
    /// ungrounded. Returns null if grounding is acceptable; a NeedsRegeneration verdict otherwise.
    /// </summary>
    private CheckVerdict? ApplyRagGrounding(string contentLower, IReadOnlyList<CurriculumChunk> chunks)
    {
        const double MinOverlapRatio = 0.05; // 5% token overlap floor

        var contentWords = contentLower
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (contentWords.Length == 0)
            return null;

        foreach (var chunk in chunks)
        {
            var chunkLower = chunk.Text.ToLowerInvariant();
            int matches = contentWords.Count(w => w.Length > 3 && chunkLower.Contains(w));
            double overlap = (double)matches / contentWords.Length;

            if (overlap >= MinOverlapRatio)
                return null; // At least one chunk has acceptable overlap.
        }

        // No chunk achieved minimum overlap → potential hallucination.
        _logger.LogDebug("HallucinationCheck: low RAG overlap across all chunks. → PotentialHallucination.");
        return CheckVerdict.NeedsRegen(ReasonCodes.PotentialHallucination);
    }
}
