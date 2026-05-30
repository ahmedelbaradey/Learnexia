namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements, Learning (and any future consumer) injects.
/// Mirrors <see cref="IStudentXpQuery"/> line-for-line in shape.
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped.
/// Returns <c>null</c> for brand-new students who have never completed a qualifying lesson.
/// Callers default to <c>Streak = 0</c> on null — clean zero-state for the dashboard.
/// </summary>
public interface IStudentStreakQuery
{
    /// <summary>
    /// Returns the streak snapshot for <paramref name="studentId"/>, or <c>null</c> if no
    /// profile exists yet (brand-new student who has never earned streak credit).
    /// </summary>
    Task<StudentStreakSnapshot?> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// Lightweight streak projection crossing the module boundary.
/// Contains only the fields that consumers need — no navigation properties, no PII.
/// <see cref="StudentId"/> is intentionally omitted (the consumer already has it — F8 clean start).
/// </summary>
public record StudentStreakSnapshot(
    int CurrentStreak,
    int LongestStreak,
    DateOnly? LastActivityDateUtc);
