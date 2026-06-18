namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module read seam: Learning implements, Parent (and Ai) inject.
/// Returns a student's weak skill areas ranked across ALL subjects with severity.
///
/// <para>This is the <strong>all-subjects</strong> seam that the Parent module's E5 endpoint
/// and the P5-01 weekly report consume.  The existing
/// <c>Ai.IStudentWeakAreasQuery</c> is <em>subject-scoped</em>; it is re-wired in the Ai DI
/// to delegate to this seam so both consumers get real data from a single implementation
/// (OQ-7 resolution from the planner).</para>
///
/// <para>Detection criteria (P5-02 AC7):</para>
/// <list type="bullet">
///   <item><c>MasteryStatus.NeedsReview</c> (mastery &lt; 50%) — always a weak area.</item>
///   <item>Recent <c>Attempt.AccuracyPercentage</c> below the low-accuracy threshold (60%)
///         AND at least one recorded attempt — contributes to ranking.</item>
/// </list>
///
/// <para>Registered in <c>AddLearningInfrastructure()</c> as Scoped.</para>
///
/// <para>Sentinel contract: a brand-new student or a student with no weak areas returns an
/// empty list — <strong>never null, never throws</strong> (AC3 / P5-02 "empty/first-week →
/// no weak areas is not an error").</para>
///
/// <para>P5-02-BE-2 (Learning seam).</para>
/// </summary>
public interface IStudentAllSubjectsWeakAreasQuery
{
    /// <summary>
    /// Returns the ranked weak areas for <paramref name="studentId"/> across all subjects.
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="maxResults">
    /// Maximum number of weak areas to return, ordered by severity then mastery deficit descending.
    /// Defaults to 5 (reasonable FE limit). Use 0 for unlimited.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A read-only list of <see cref="WeakAreaEntry"/>, ordered by severity (High first) then by
    /// mastery deficit descending. Empty list — never null — when no weak areas are detected.
    /// </returns>
    Task<IReadOnlyList<WeakAreaEntry>> GetWeakAreasAsync(
        int studentId,
        int maxResults = 5,
        CancellationToken ct = default);
}

/// <summary>
/// A single weak curriculum area for a student, as ranked by the P5-02 detector.
/// </summary>
/// <param name="SkillId">Intra-Learning skill identifier.</param>
/// <param name="SkillName">Human-readable skill name (as stored in the curriculum).</param>
/// <param name="SubjectCode">
/// The subject this skill belongs to, as an <c>int</c> matching
/// <c>Learning.Domain.Enums.SubjectCode</c> values (0=MATH, 1=SCIENCE, 2=ARABIC, 3=ENGLISH).
/// </param>
/// <param name="MasteryPercent">Current mastery percentage [0, 100] for this skill.</param>
/// <param name="Severity">How severe the weakness is (High / Medium / Low).</param>
/// <param name="SuggestedNextAction">
/// A localisation-key identifier for the suggested next action (e.g. practice this skill,
/// review the concept). Consumers resolve via localizer if displayed.
/// </param>
public record WeakAreaEntry(
    int SkillId,
    string SkillName,
    int SubjectCode,
    int MasteryPercent,
    WeakAreaSeverity Severity,
    string SuggestedNextAction);

/// <summary>
/// Severity level of a weak area as produced by the P5-02 detector.
///
/// <para>Mapping rule (detector):</para>
/// <list type="bullet">
///   <item><see cref="High"/>   — mastery &lt; 30% (deeply stuck).</item>
///   <item><see cref="Medium"/> — 30% ≤ mastery &lt; 50% (NeedsReview range, needs work).</item>
///   <item><see cref="Low"/>    — mastery ≥ 50% but recent attempt accuracy &lt; 60% (drifting).</item>
/// </list>
/// Values are explicitly assigned — stored as int; never use magic int literals.
/// </summary>
public enum WeakAreaSeverity
{
    Low    = 1,
    Medium = 2,
    High   = 3,
}
