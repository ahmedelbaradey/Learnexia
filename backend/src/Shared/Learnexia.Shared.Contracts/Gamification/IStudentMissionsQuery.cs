namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements, Learning (and any future consumer) injects.
/// Mirrors <see cref="IStudentBadgesQuery"/> line-for-line in shape — P4-06.
///
/// Returns a sentinel snapshot ([], null) for brand-new students — never null (D2).
/// Lazy instantiation of the current-period <see cref="StudentMissionsSnapshot"/> missions
/// is triggered here on first call per period (D2 decision).
/// This avoids leaking Gamification.Domain types into the Learning module (module isolation rule 1).
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped.
/// </summary>
public interface IStudentMissionsQuery
{
    /// <summary>
    /// Returns the missions snapshot for <paramref name="studentId"/>.
    /// For brand-new students who have never loaded the dashboard, returns a sentinel snapshot with
    /// <c>Daily = []</c> and <c>Weekly = null</c>.
    /// Never returns null — the sentinel guarantees callers always have a valid missions value.
    /// Lazy-instantiates the current-period missions if no rows exist for this period (D2 / AC4).
    /// </summary>
    Task<StudentMissionsSnapshot> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// Lightweight missions projection crossing the module boundary (P4-06).
/// Contains only the fields that consumers need — no navigation properties, no PII.
/// </summary>
/// <param name="Daily">All daily missions for the current period. Empty when none instantiated yet.</param>
/// <param name="Weekly">The first (highest-priority) weekly mission for the current period. Null when none instantiated yet.</param>
public sealed record StudentMissionsSnapshot(
    IReadOnlyList<MissionSummary> Daily,
    MissionSummary? Weekly);

/// <summary>
/// Summary of a single mission instance — used in the dashboard and missions endpoint (P4-06).
/// </summary>
/// <param name="Code">Stable mission code (e.g., "DAILY_3_LESSONS").</param>
/// <param name="IconKey">FE asset bundle key (e.g., "icon.mission.daily_3_lessons").</param>
/// <param name="TitleKey">FE i18n title key (e.g., "mission.daily_3_lessons.title").</param>
/// <param name="TargetType">What the student must do to progress (mirrored enum — values match Gamification.Domain.Enums.MissionTargetType).</param>
/// <param name="Progress">Current progress integer.</param>
/// <param name="Target">Target integer (completion threshold).</param>
/// <param name="Status">Mission lifecycle state (mirrored enum — values match Gamification.Domain.Enums.MissionStatus).</param>
/// <param name="CompletedAtUtc">UTC time the mission was completed, or null if not yet completed.</param>
public sealed record MissionSummary(
    string Code,
    string IconKey,
    string TitleKey,
    MissionTargetTypeDto TargetType,
    int Progress,
    int Target,
    MissionStatusDto Status,
    DateTime? CompletedAtUtc);

/// <summary>
/// Mirror of <c>Learnexia.Modules.Gamification.Domain.Enums.MissionType</c> for cross-module use.
/// Integer values MUST remain in sync with the Domain enum.
/// A compile-time assertion in the integration tests pins both to the same integers.
/// </summary>
public enum MissionTypeDto
{
    Daily  = 1,
    Weekly = 2,
}

/// <summary>
/// Mirror of <c>Learnexia.Modules.Gamification.Domain.Enums.MissionStatus</c> for cross-module use.
/// Integer values MUST remain in sync with the Domain enum.
/// A compile-time assertion in the integration tests pins both to the same integers.
/// </summary>
public enum MissionStatusDto
{
    NotStarted = 0,
    InProgress = 1,
    Completed  = 2,
    Expired    = 3,
}

/// <summary>
/// Mirror of <c>Learnexia.Modules.Gamification.Domain.Enums.MissionTargetType</c> for cross-module use.
/// Integer values MUST remain in sync with the Domain enum.
/// A compile-time assertion in the integration tests pins both to the same integers.
/// </summary>
public enum MissionTargetTypeDto
{
    CompleteLessons = 1,
    CorrectAnswers  = 2,
    MaintainStreak  = 3,
}
