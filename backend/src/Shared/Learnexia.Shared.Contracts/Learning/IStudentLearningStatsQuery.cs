namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module read seam: Learning implements, Parent (and any other consumer) injects.
/// Returns aggregated learning statistics for a given student over a UTC time window.
///
/// <para>Registered in <c>AddLearningInfrastructure()</c> as Scoped.</para>
///
/// <para>All results are sentinel-safe: a brand-new student or an empty window returns a zeroed
/// <see cref="LearningStats"/> with empty collections — never null, never throws. The
/// <see cref="GetLastActivityUtcAsync"/> overload returns null when the student has never
/// completed an attempt (caller defaults to activeToday = false).</para>
///
/// <para>P5-08-BE-2 (Learning batch 1b).</para>
/// </summary>
public interface IStudentLearningStatsQuery
{
    /// <summary>
    /// Returns aggregated learning stats for <paramref name="studentId"/> over
    /// [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    /// </summary>
    /// <param name="studentId">Loose int reference to the Identity student user (no cross-module FK).</param>
    /// <param name="fromUtc">Window start (inclusive), UTC.</param>
    /// <param name="toUtc">Window end (exclusive), UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="LearningStats"/> value — never null. Returns a zeroed instance when the
    /// student has no data in the requested window (first-week / brand-new student — AC3).
    /// </returns>
    Task<LearningStats> GetStatsAsync(
        int studentId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the UTC timestamp of the most recent completed <c>Attempt</c> for
    /// <paramref name="studentId"/>, or <c>null</c> if the student has never completed an attempt.
    /// Callers use this to derive <c>activeToday</c> (compare date with today's UTC date).
    /// </summary>
    Task<DateTime?> GetLastActivityUtcAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// Aggregated learning statistics for a student over a UTC time window.
/// Produced by <see cref="IStudentLearningStatsQuery"/>.
/// </summary>
/// <param name="LessonsCompleted">
/// Count of <em>distinct</em> lesson IDs where the student has a <c>Completed</c> attempt
/// within the window. Mirrors AC6: "lessonsCompleted = distinct completed lessons".
/// </param>
/// <param name="TotalAttempts">Total attempt records (any status) in the window.</param>
/// <param name="TimeLearningSeconds">
/// Sum of <c>Attempt.DurationSeconds</c> for completed attempts in the window.
/// Callers divide by 60 for minutes display (AC6 / brief OQ-2).
/// </param>
/// <param name="AvgAccuracy">
/// Mean <c>Attempt.AccuracyPercentage</c> across completed attempts in the window.
/// Zero when no completed attempts exist.
/// </param>
public record LearningStats(
    int LessonsCompleted,
    int TotalAttempts,
    int TimeLearningSeconds,
    double AvgAccuracy);
