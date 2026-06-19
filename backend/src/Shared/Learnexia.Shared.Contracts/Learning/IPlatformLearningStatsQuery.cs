namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module platform-aggregate read seam: Learning implements, façade consumers inject.
/// Returns platform-wide completion and activity statistics over a UTC time window.
///
/// <para>Registered in <c>AddLearningInfrastructure()</c> as Scoped.</para>
///
/// <para>P7-10 analytics dashboard (option a — honest v1 over existing data).
/// All results are sentinel-safe: an empty window returns zeroed/empty stats — never null, never throws.</para>
///
/// <para>OQ-2 (brief): the Learning module has only one attempt type (<c>Attempt</c>).
/// Every quiz attempt is indistinguishable at the row level from a "lesson attempt" —
/// they are the SAME entity. Therefore <c>QuizzesCompleted</c> is NOT a separate seam result;
/// instead a single attempt = one lesson's quiz session. The façade labels this as
/// <c>LessonsCompleted</c> (distinct lessons with completed attempt) and surfaces it accordingly.
/// Quizzes-completed as a separate counter is NOT derivable from current data and is marked N/A in the KPI DTO.</para>
/// </summary>
public interface IPlatformLearningStatsQuery
{
    /// <summary>
    /// Returns platform-wide aggregated learning stats over [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// </summary>
    /// <param name="fromUtc">Window start (inclusive), UTC.</param>
    /// <param name="toUtc">Window end (exclusive), UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="PlatformLearningStats"/> value — never null.
    /// Returns a zeroed/empty instance when no data exists in the window.
    /// </returns>
    Task<PlatformLearningStats> GetPlatformAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);
}

/// <summary>
/// Platform-wide aggregated learning statistics over a UTC time window.
/// Produced by <see cref="IPlatformLearningStatsQuery"/>.
/// </summary>
/// <param name="LessonsCompleted">
/// Count of distinct lesson IDs with at least one <c>Completed</c> attempt in the window.
/// </param>
/// <param name="TotalAttempts">
/// Total completed attempt records (any lesson) in the window.
/// </param>
/// <param name="DistinctActiveStudents">
/// Count of distinct student IDs with at least one <c>Completed</c> attempt in the window.
/// This is an <b>activity proxy</b> for DAU/WAU/MAU — not a true session metric (P5-03 needed for that).
/// </param>
/// <param name="BySubject">
/// Per-<see cref="SubjectBreakdown"/> aggregation (subject code + language).
/// Empty when no data exists.
/// </param>
/// <param name="ByGrade">
/// Per-<see cref="GradeBreakdown"/> aggregation.
/// Empty when no data exists.
/// </param>
/// <param name="ByLanguage">
/// Per-<see cref="LanguageBreakdown"/> aggregation (ar/en). The curriculum is bilingual
/// parallel trees, so language is a first-class breakdown dimension (story P7-10 AC).
/// Empty when no data exists.
/// </param>
public record PlatformLearningStats(
    int LessonsCompleted,
    int TotalAttempts,
    int DistinctActiveStudents,
    IReadOnlyList<SubjectBreakdown> BySubject,
    IReadOnlyList<GradeBreakdown> ByGrade,
    IReadOnlyList<LanguageBreakdown> ByLanguage);

/// <summary>
/// Completion counts for a single subject (identified by code + language) in the analytics window.
/// </summary>
/// <param name="SubjectCode">Stable curriculum subject code (int-stored enum value).</param>
/// <param name="Language">Content language (0 = Ar, 1 = En).</param>
/// <param name="LessonsCompleted">Distinct lessons completed within this subject tree.</param>
/// <param name="TotalAttempts">Total completed attempts within this subject tree.</param>
/// <param name="DistinctActiveStudents">Distinct students active within this subject tree.</param>
public record SubjectBreakdown(
    int SubjectCode,
    int Language,
    int LessonsCompleted,
    int TotalAttempts,
    int DistinctActiveStudents);

/// <summary>
/// Completion counts for a single grade in the analytics window.
/// </summary>
/// <param name="GradeId">Grade identifier.</param>
/// <param name="LessonsCompleted">Distinct lessons completed within this grade.</param>
/// <param name="TotalAttempts">Total completed attempts within this grade.</param>
/// <param name="DistinctActiveStudents">Distinct students active within this grade.</param>
public record GradeBreakdown(
    int GradeId,
    int LessonsCompleted,
    int TotalAttempts,
    int DistinctActiveStudents);

/// <summary>
/// Completion counts for a single curriculum language (ar/en) in the analytics window.
/// Lets an admin compare engagement/throughput across the two parallel-tree languages.
/// </summary>
/// <param name="Language">Content language (0 = Ar, 1 = En).</param>
/// <param name="LessonsCompleted">Distinct lessons completed within this language tree.</param>
/// <param name="TotalAttempts">Total completed attempts within this language tree.</param>
/// <param name="DistinctActiveStudents">Distinct students active within this language tree.</param>
public record LanguageBreakdown(
    int Language,
    int LessonsCompleted,
    int TotalAttempts,
    int DistinctActiveStudents);
