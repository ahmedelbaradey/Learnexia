namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements, Learning (and any future consumer) injects.
/// Mirrors <see cref="IStudentMissionsQuery"/> line-for-line in shape — P4-07.
///
/// Returns a sentinel snapshot for brand-new students with no XP profile yet — never null (D13).
/// Lazy-instantiates the current-week <see cref="StudentLeagueSnapshot"/> membership on first call
/// per period, mirroring the <see cref="IStudentMissionsQuery"/> sentinel + lazy-instantiation pattern.
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped.
/// </summary>
public interface IStudentLeagueQuery
{
    /// <summary>
    /// Returns the league snapshot for <paramref name="studentId"/>.
    ///
    /// For brand-new students who have never loaded the dashboard (no XP profile yet), returns
    /// a sentinel snapshot: <c>(Tier: Bronze, WeeklyXp: 0, CurrentRank: 0, GroupSize: 0)</c>.
    /// Never returns null — the sentinel guarantees callers always have a valid league value.
    ///
    /// Lazy-instantiates the current-week league membership if none exists for this period (D12 / AC1).
    /// Race safety: the unique index on (StudentXpProfileId, PeriodKey) prevents duplicate
    /// instantiation under concurrent dashboard reads; the <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>
    /// catch treats a constraint violation as "already instantiated by a concurrent request" and re-reads.
    /// </summary>
    Task<StudentLeagueSnapshot> GetByStudentIdAsync(int studentId, CancellationToken ct = default);
}

/// <summary>
/// Lightweight league projection crossing the module boundary (P4-07).
/// Contains only the fields that consumers (dashboard preview) need.
/// No navigation properties, no PII, no Domain types.
/// </summary>
/// <param name="Tier">The student's current league tier.</param>
/// <param name="WeeklyXp">XP earned in the current week's competition period.</param>
/// <param name="CurrentRank">1-based rank within the cohort (0 = sentinel for brand-new student).</param>
/// <param name="GroupSize">Total participants in the cohort (0 = sentinel for brand-new student).</param>
public sealed record StudentLeagueSnapshot(
    LeagueTierDto Tier,
    int WeeklyXp,
    int CurrentRank,
    int GroupSize);

/// <summary>
/// Mirror of <c>Learnexia.Modules.Gamification.Domain.Enums.LeagueTier</c> for cross-module use.
/// Integer values MUST remain in sync with the Domain enum.
/// A compile-time assertion in the integration tests pins both to the same integers.
///
/// <para>D6 locked decision: Bronze=1, Silver=2, Gold=3, Diamond=4 — exactly 4 tiers per FR-GM-6.</para>
/// </summary>
public enum LeagueTierDto
{
    Bronze  = 1,
    Silver  = 2,
    Gold    = 3,
    Diamond = 4,
}
