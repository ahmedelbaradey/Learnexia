namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module read seam: Learning implements, Parent (dashboard) and Ai (Lexi narration P3-14) inject.
/// Returns the most recently persisted daily recommendation set for a student.
///
/// <para>Sentinel contract: always returns a non-null, non-throwing result.
/// A brand-new student or a student with no computed recommendations returns an empty list —
/// never null, never throws (mirror <see cref="IStudentAllSubjectsWeakAreasQuery"/>).</para>
///
/// <para>Registered in <c>AddLearningInfrastructure()</c> as Scoped.</para>
///
/// <para>P5-09-BE-5.</para>
/// </summary>
public interface IStudentRecommendationsQuery
{
    /// <summary>
    /// Returns the most recently persisted <see cref="RecommendationItem"/> list for the given student.
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A read-only list of <see cref="RecommendationItem"/>.
    /// Empty list when no recommendation row has been computed yet.
    /// Never null, never throws.
    /// </returns>
    Task<IReadOnlyList<RecommendationItem>> GetLatestAsync(
        int studentId,
        CancellationToken ct = default);
}

/// <summary>
/// A single actionable recommendation item in a student's daily recommendation set (P5-09).
///
/// <para>All text fields (<see cref="TitleKey"/>, <see cref="BodyKey"/>, <see cref="CtaKey"/>)
/// carry i18n resource keys — never rendered text. Consumers resolve them through the
/// string localizer. This enables EN+AR rendering without re-persisting on language change.</para>
///
/// <para>The record lives here in <c>Shared.Contracts/Learning</c> so it is the single
/// definition shared by the engine (P5-09-BE-2), the seam (P5-09-BE-5), and the Lexi narration
/// grounding (P3-14-BE-3) — no duplication, no mapping step.</para>
/// </summary>
/// <param name="SkillId">
/// Intra-Learning curriculum skill identifier this recommendation targets.
/// </param>
/// <param name="SubjectCode">
/// The subject this skill belongs to, as an <c>int</c> matching
/// <c>Learning.Domain.Enums.SubjectCode</c> values (0=MATH, 1=SCIENCE, 2=ARABIC, 3=ENGLISH).
/// </param>
/// <param name="TitleKey">
/// i18n resource key for the recommendation title (e.g. "Rec_PracticeSkill_Title").
/// </param>
/// <param name="BodyKey">
/// i18n resource key for the recommendation body text.
/// </param>
/// <param name="CtaKey">
/// i18n resource key for the call-to-action label (e.g. "Rec_Practice_Cta").
/// </param>
/// <param name="Severity">
/// Severity of the underlying weakness as an int. Maps to
/// <c>Learning.Shared.Contracts.Learning.WeakAreaSeverity</c>
/// (1=Low, 2=Medium, 3=High).
/// </param>
/// <param name="ActionType">
/// The recommended action type as <see cref="RecommendationActionType"/>.
/// </param>
/// <param name="TargetDifficulty">
/// Target difficulty for the recommended practice session as an int matching
/// <c>Learning.Domain.Enums.DifficultyLevel</c> (1=Easy, 2=Medium, 3=Hard).
/// Derived from <c>IAdaptivityService.GetTargetDifficulty</c> for the skill.
/// </param>
/// <param name="PreferredExplanationStyle">
/// Optional soft passthrough of the student's derived explanation-style preference (P5-09a-BE-3).
/// Nullable — null when the profile is cold-start or the style is unknown.
/// Carried for P3-14a so Lexi can honour the preference without a cross-module profile call.
/// The engine never acts on this field as a hard driver — it is a soft hint only.
/// Values mirror <c>Learning.Domain.Enums.ExplanationStyle</c> (Standard=0, Simplified=1, Visual=2, StepByStep=3).
/// </param>
public record RecommendationItem(
    int SkillId,
    int SubjectCode,
    string TitleKey,
    string BodyKey,
    string CtaKey,
    int Severity,
    RecommendationActionType ActionType,
    int TargetDifficulty,
    RecommendationExplanationStyle? PreferredExplanationStyle = null);

/// <summary>
/// Explanation-style preference carried on a <see cref="RecommendationItem"/> (P5-09a-BE-3).
///
/// Mirrors <c>Learning.Domain.Enums.ExplanationStyle</c> values exactly so that Shared.Contracts
/// consumers (Lexi / Ai module) can read the preference without a cross-module domain reference.
/// Values are explicitly assigned to guarantee stable serialization order.
/// </summary>
public enum RecommendationExplanationStyle
{
    /// <summary>
    /// Default / cold-start. Standard textual explanation; no special adaptation.
    /// </summary>
    Standard   = 0,

    /// <summary>
    /// Simplified language and shorter sentences.
    /// </summary>
    Simplified = 1,

    /// <summary>
    /// Visual / diagram-oriented explanation.
    /// </summary>
    Visual     = 2,

    /// <summary>
    /// Step-by-step breakdown.
    /// </summary>
    StepByStep = 3,
}

/// <summary>
/// Recommended action type for a <see cref="RecommendationItem"/>.
/// Stored as int; referenced by enum member, never a magic string/int.
/// Values are explicitly assigned — never assume default ordinal ordering.
/// </summary>
public enum RecommendationActionType
{
    /// <summary>
    /// The student should practice questions for this skill at the target difficulty.
    /// Typical for Medium/Low severity weak areas.
    /// </summary>
    Practice = 1,

    /// <summary>
    /// The student should review the underlying concept before attempting new questions.
    /// Typical for High severity weak areas (mastery below 30%).
    /// </summary>
    Review = 2,

    /// <summary>
    /// The student is doing well; encourage them to keep the streak alive on this skill.
    /// Used when a recently-good skill is at risk of regressing.
    /// </summary>
    KeepStreak = 3,

    /// <summary>
    /// The student has achieved strong mastery; a celebratory framing.
    /// Used in the encouraging cold-start / well-done path.
    /// </summary>
    Celebrate = 4,
}
