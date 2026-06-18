using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;

namespace Learnexia.Modules.Ai.Application.PromptBuilder;

/// <summary>
/// Input context for <see cref="IPromptBuilder.Build"/>. Carries all data the builder
/// needs to assemble a personalised, child-safe prompt.
///
/// <para><strong>PII minimisation rule (security-auditor gate):</strong>
/// this record carries <see cref="StudentId"/> for seam lookups performed BEFORE <c>Build</c>
/// is called (e.g. fetching weak areas). The builder MUST NOT inject <see cref="StudentId"/>
/// into the assembled prompt text — only the anonymous proxies <see cref="Grade"/> and
/// <see cref="Age"/> may appear in the prompt body.</para>
///
/// <para><strong>P3-14a — un-conflated enrichment fields:</strong>
/// <list type="bullet">
///   <item><see cref="CurrentLevel"/> carries the gamification level for motivational framing ONLY.
///     Never used to select which areas appear or to set difficulty.</item>
///   <item><see cref="EncouragementStyle"/> carries an anonymous, coarse style hint derived from the
///     student's <c>PreferredExplanationStyle</c> persisted on the recommendation item (P5-09a).
///     No raw behavioral data (no skill error lists, no <c>StudentId</c>) ever reaches the prompt.</item>
/// </list>
/// Both fields are used ONLY for <see cref="HelperIntent.Recommendation"/> and ignored for all other
/// intents.</para>
/// </summary>
/// <param name="StudentId">
/// The internal student identifier. Used by callers to query seams (e.g. weak areas)
/// BEFORE calling <c>Build</c>. MUST NOT appear in the assembled prompt text.
/// </param>
/// <param name="Intent">The validated helper intent (one of the four allowed intents).</param>
/// <param name="Subject">The curriculum subject for this request (exactly 4 members, no Social Studies).</param>
/// <param name="Grade">The student's current school grade (anonymous proxy — used in prompt text).</param>
/// <param name="Age">The student's approximate age in years (anonymous proxy — used in prompt text).</param>
/// <param name="Language">The student's medium-of-instruction language preference.</param>
/// <param name="WeakAreas">
/// Optional weak curriculum areas. When null or empty the builder omits the weak-areas
/// section entirely (AC4 graceful degradation). Populated from <c>IStudentWeakAreasQuery</c>
/// by the calling handler before invoking <c>Build</c>.
/// </param>
/// <param name="Context">
/// Optional learning context (curriculum chunks, question text, wrong answer, skill metadata).
/// When null or its <c>Chunks</c> collection is empty the builder omits the context section
/// entirely (AC4 graceful degradation). Populated from <c>ILearningContextProvider</c>
/// by the calling handler before invoking <c>Build</c>.
/// </param>
/// <param name="CurrentLevel">
/// P3-14a: the student's current gamification level (anonymous proxy — used in motivational framing
/// for <see cref="HelperIntent.Recommendation"/> ONLY). Defaults to 1 when not supplied.
/// MUST NOT be used to select areas or set difficulty (un-conflation rule).
/// MUST NOT appear as a raw identifier in the prompt — only the integer value is emitted as a
/// motivational framing hint.
/// </param>
/// <param name="EncouragementStyle">
/// P3-14a: optional anonymous, coarse style hint derived from the student's persisted
/// <c>PreferredExplanationStyle</c> on the <c>RecommendationItem</c> (P5-09a). Null when
/// the profile is cold-start or the style is unknown. Used ONLY for
/// <see cref="HelperIntent.Recommendation"/>. Never carries raw behavioral data or StudentId.
/// </param>
public sealed record PromptContext(
    int StudentId,
    HelperIntent Intent,
    Subject Subject,
    int Grade,
    int Age,
    TutorLanguage Language,
    IReadOnlyList<WeakArea>? WeakAreas,
    LearningContext? Context,
    int CurrentLevel = 1,
    EncouragementStyle? EncouragementStyle = null);

/// <summary>
/// P3-14a: anonymous, coarse encouragement-style hint derived from the student's persisted
/// <c>RecommendationExplanationStyle</c> (P5-09a). Used only for the Recommendation narration
/// prompt framing. This is an internal Ai-module enum; it never leaves the prompt assembly layer
/// and is never stored or logged.
///
/// <para>Mapping from <c>RecommendationExplanationStyle</c>:
/// Standard → <see cref="Balanced"/>;
/// Simplified → <see cref="Short"/>;
/// Visual → <see cref="Balanced"/>;
/// StepByStep → <see cref="Detailed"/>.
/// </para>
/// </summary>
public enum EncouragementStyle
{
    /// <summary>
    /// Default encouragement — balanced praise and clarity. Maps from Standard / Visual styles.
    /// </summary>
    Balanced = 0,

    /// <summary>
    /// Short and very warm encouragement — suitable for fatigue-prone or simplified-preference learners.
    /// Maps from Simplified style.
    /// </summary>
    Short = 1,

    /// <summary>
    /// Detailed and step-oriented encouragement — suitable for learners who prefer step-by-step guidance.
    /// Maps from StepByStep style.
    /// </summary>
    Detailed = 2,
}
