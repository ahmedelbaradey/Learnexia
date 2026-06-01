using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Append-only idempotency ledger for league XP increments. One row per
/// (LeagueMembershipId, OriginEventId) — enforced by DB unique constraint
/// <c>UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId</c>.
///
/// A redelivered <c>XpAwardedDomainEvent</c> is detected by pre-checking this table before
/// calling <see cref="LeagueMembership.AddWeeklyXp"/>; the DB constraint is the backstop for
/// concurrent races (D9).
///
/// Mirrors <see cref="MissionProgressLog"/> pattern line-for-line.
/// Derives from <see cref="CreationAuditedEntity"/> — strictly append-only, no mutation after insert.
/// </summary>
public sealed class LeagueXpDeltaLog : CreationAuditedEntity
{
    /// <summary>FK to <see cref="LeagueMembership"/> (CASCADE delete).</summary>
    public int LeagueMembershipId { get; private set; }

    /// <summary>Navigation property — resolved by EF at SaveChanges time.</summary>
    public LeagueMembership LeagueMembership { get; private set; } = null!;

    /// <summary>
    /// EventId from the originating <c>XpAwardedDomainEvent</c>.
    /// One row per (LeagueMembershipId, OriginEventId) — DB unique constraint backstop.
    /// </summary>
    public Guid OriginEventId { get; private set; }

    /// <summary>XP amount credited to <see cref="LeagueMembership.WeeklyXp"/> by this event.</summary>
    public int Delta { get; private set; }

    /// <summary>Timestamp from the originating event (<c>XpAwardedDomainEvent.OccurredAtUtc</c>).</summary>
    public DateTime OccurredAtUtc { get; private set; }

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private LeagueXpDeltaLog() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new idempotency log row. Takes the navigation reference to
    /// <paramref name="membership"/> so EF Core can resolve the real FK at SaveChanges time
    /// (mirrors <see cref="MissionProgressLog.Create"/> pattern).
    /// </summary>
    public static LeagueXpDeltaLog Create(
        LeagueMembership membership,
        Guid originEventId,
        int delta,
        DateTime occurredAtUtc)
        => new()
        {
            LeagueMembership = membership,
            OriginEventId = originEventId,
            Delta = delta,
            OccurredAtUtc = occurredAtUtc,
        };
}
