using Learnexia.Modules.Learning.Domain.Enums;

namespace Learnexia.Modules.Learning.Application.Features.Profile.Dtos;

/// <summary>
/// Response DTO for the student behavioral profile inspection endpoint (P3-13 BE-8 / AC5).
///
/// DATA MINIMIZATION (child privacy — MANDATORY):
/// This DTO exposes ONLY the four AC1 derived attributes plus two confidence indicators.
/// It MUST NOT include:
/// - Raw answer data (individual answers, payloads, timestamps of individual answers)
/// - Raw session logs
/// - Any PII beyond what is inherent to the derived attributes
/// - Gamification fields (XP, streak, hearts — those belong to the Gamification module)
///
/// Consumers (P3-03 prompt builder, P3-08 adaptivity engine) should treat
/// <see cref="DataPointCount"/> below the cold-start threshold as "insufficient data".
/// </summary>
public class StudentLearningProfileDto
{
    /// <summary>
    /// Per-QuestionType success-rate affinity map. Keys are QuestionType enum names;
    /// values are success rates [0.0 .. 1.0]. Empty when insufficient data.
    /// AC1 attribute 1: question-type affinity.
    /// </summary>
    public IReadOnlyDictionary<string, double> QuestionTypeAffinity { get; set; }
        = new Dictionary<string, double>();

    /// <summary>
    /// List of SkillIds where the student has recurring wrong-answer patterns.
    /// Empty when no recurring errors are detected or on cold-start.
    /// AC1 attribute 2: recurring-error clusters.
    /// </summary>
    public IReadOnlyList<int> RecurringErrorSkillIds { get; set; } = Array.Empty<int>();

    /// <summary>
    /// Estimated minutes into a session before accuracy noticeably drops.
    /// Null when insufficient data (cold-start or too few attempts).
    /// AC1 attribute 3: attention-span / session-fatigue proxy.
    /// v1: derived from Attempt/StudentAnswer timestamps (P2-08 proxy).
    /// </summary>
    public int? AttentionSpanMinutes { get; set; }

    /// <summary>
    /// Derived preferred explanation style. Standard = cold-start / no pattern detected.
    /// Provisional — values pending confirmation against P3-03 prompt builder.
    /// AC1 attribute 4: preferred explanation style.
    /// </summary>
    public ExplanationStyle PreferredExplanationStyle { get; set; } = ExplanationStyle.Standard;

    /// <summary>
    /// Number of answer data points that fed the most recent derivation.
    /// 0 = cold-start (no data). Consumers treat values below the configured threshold
    /// as "insufficient data — do not adapt yet."
    /// Confidence indicator; NOT raw answer data.
    /// </summary>
    public int DataPointCount { get; set; }
}
