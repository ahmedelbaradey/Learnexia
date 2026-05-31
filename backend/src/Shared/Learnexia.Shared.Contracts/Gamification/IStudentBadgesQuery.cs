namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements, Learning (and any future consumer) injects.
/// Mirrors <see cref="IStudentHeartsQuery"/> line-for-line in shape — P4-05.
///
/// Returns a sentinel snapshot (0, []) for brand-new students — never null (D2).
/// This avoids leaking Gamification.Domain types into the Learning module (module isolation rule 1).
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped.
/// </summary>
public interface IStudentBadgesQuery
{
    /// <summary>
    /// Returns the badges snapshot for <paramref name="studentId"/>.
    /// For brand-new students who have never earned a badge, returns a sentinel snapshot with
    /// <c>TotalCount = 0</c> and <c>Recent = []</c>.
    /// Never returns null — the sentinel guarantees callers always have a valid badges value.
    /// </summary>
    Task<StudentBadgesSnapshot> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// Lightweight badges projection crossing the module boundary (P4-05).
/// Contains only the fields that consumers need — no navigation properties, no PII.
/// </summary>
/// <param name="TotalCount">Total number of badges earned by the student.</param>
/// <param name="Recent">Top 3 most recently earned badges (ordered by AwardedAtUtc DESC). Empty when none earned.</param>
public sealed record StudentBadgesSnapshot(
    int TotalCount,
    IReadOnlyList<BadgeSummary> Recent);

/// <summary>
/// Summary of a single earned badge — used in the dashboard recent-3 strip (P4-05 D2).
/// </summary>
/// <param name="Code">Stable badge code (e.g., "FIRST_LESSON").</param>
/// <param name="IconKey">FE asset bundle key (e.g., "icon.first_lesson").</param>
/// <param name="Rarity">Badge rarity tier (mirrored enum — values match Gamification.Domain.Enums.BadgeRarity).</param>
/// <param name="AwardedAtUtc">UTC time the badge was awarded.</param>
public sealed record BadgeSummary(
    string Code,
    string IconKey,
    BadgeRarityDto Rarity,
    DateTime AwardedAtUtc);

/// <summary>
/// Mirror of <c>Learnexia.Modules.Gamification.Domain.Enums.BadgeRarity</c> for cross-module use (D10).
/// Integer values MUST remain in sync with the Domain enum.
/// A compile-time assertion in the integration tests pins both to the same integers.
/// </summary>
public enum BadgeRarityDto
{
    Common    = 1,
    Rare      = 2,
    Epic      = 3,
    Legendary = 4,
}
