namespace Learnexia.Shared.Contracts.Gamification;

/// <summary>
/// Cross-module read seam: Gamification implements, Learning (and any future consumer) injects.
/// Mirrors <see cref="IStudentXpQuery"/> line-for-line in shape — P4-11-B1-B.
///
/// Returns the list of events whose window currently contains <paramref name="atUtc"/>.
/// Used by <c>IXpBoostCalculator</c> (Batch 2) and by the dashboard handler (Batch 4) to populate
/// the timed-event banner. Returns an empty list (never null) when no events are active.
///
/// Registered in <c>AddGamificationInfrastructure()</c> as Scoped; resolved as a
/// <c>CachedActiveTimedEventsQuery</c> decorator wrapping <c>PostgresActiveTimedEventsQuery</c>.
/// </summary>
public interface IActiveTimedEventsQuery
{
    /// <summary>
    /// Returns all active timed-event snapshots valid at <paramref name="atUtc"/>.
    /// Never returns null — returns an empty list when no events are active.
    /// </summary>
    Task<IReadOnlyList<ActiveTimedEventSnapshot>> GetActiveAtAsync(
        DateTime atUtc, CancellationToken ct = default);
}

/// <summary>
/// Lightweight timed-event projection crossing the module boundary (P4-11-B1-B).
/// Contains only the fields consumers need — no navigation properties, no PII.
///
/// <see cref="Scope"/> is stored as <see cref="TimedEventScopeDto"/> (mirror of the Domain enum)
/// so Learning and other consumers can interpret it without referencing Gamification.Domain.
/// Integer values are identical to <c>Learnexia.Modules.Gamification.Domain.Enums.TimedEventScope</c>.
/// </summary>
public sealed record ActiveTimedEventSnapshot(
    int Id,
    string Code,
    string NameEn,
    string NameAr,
    decimal Multiplier,
    TimedEventScopeDto Scope,
    DateTime StartUtc,
    DateTime EndUtc);

/// <summary>
/// Mirror of <c>Learnexia.Modules.Gamification.Domain.Enums.TimedEventScope</c> for cross-module use.
/// Integer values MUST remain in sync with the Domain enum.
/// A compile-time assertion in the integration tests pins both to the same integers.
/// </summary>
public enum TimedEventScopeDto
{
    /// <summary>Multiplier applies to every XP award regardless of reason.</summary>
    AllXp = 1,

    /// <summary>Multiplier applies only to XP from mission completions (forward-compat — P5+).</summary>
    MissionXp = 2,

    /// <summary>Multiplier applies only to XP from league contributions (forward-compat — P5+).</summary>
    LeagueXp = 3,
}
