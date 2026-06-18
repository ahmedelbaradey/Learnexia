using Learnexia.Shared.Contracts.Learning;

namespace Learnexia.Modules.Learning.Application.Abstractions;

/// <summary>
/// In-module service seam for the P5-02 weak-area detector.
/// Application handlers and the <see cref="IStudentAllSubjectsWeakAreasQuery"/> infrastructure
/// adapter inject this interface — never EF directly (Option C).
///
/// <para>Detection criteria:</para>
/// <list type="bullet">
///   <item><c>MasteryStatus.NeedsReview</c> (mastery &lt; 50 %) — always a weak area.</item>
///   <item>Recent attempt accuracy &lt; 60 % AND at least one attempt — contributes to ranking.</item>
/// </list>
///
/// <para>Empty / first-week safety: returns an empty list (never null) when the student has no
/// qualifying weak areas — this is AC3 / P5-02 acceptance criterion "no weak areas is not an
/// error".</para>
///
/// <para>P5-02-BE-1 (Learning detector service).</para>
/// </summary>
public interface IWeakAreaDetectorService
{
    /// <summary>
    /// Detects and ranks a student's weak skill areas across all subjects.
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user.</param>
    /// <param name="maxResults">
    /// Maximum number of weak areas to return (ordered by severity desc, then mastery deficit desc).
    /// Use 0 for unlimited.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Ranked list of <see cref="WeakAreaEntry"/> — empty list when no weak areas detected.
    /// Never null.
    /// </returns>
    Task<IReadOnlyList<WeakAreaEntry>> DetectAsync(
        int studentId,
        int maxResults = 5,
        CancellationToken ct = default);
}
