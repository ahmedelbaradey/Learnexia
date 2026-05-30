using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Append-only ledger row for every XP award. One row per discrete award event.
/// Idempotency is enforced via a DB unique constraint on
/// <c>UX_XpAwards_OriginEventId_Reason</c> — a redelivered integration event with
/// the same <see cref="OriginEventId"/> and same <see cref="Reason"/> will be rejected
/// at the database layer (AC4).
///
/// Soft references: <see cref="LessonId"/> and <see cref="SkillId"/> are plain nullable
/// ints — no cross-module FK constraints (module isolation rule 1).
///
/// NOT derived from <see cref="AggregateRoot"/> — this is a ledger row, not an aggregate
/// root with domain events. <see cref="FullAuditedEntity"/> (CreatedAt/By stamps) is sufficient.
/// No mutation methods — the ledger is strictly append-only.
/// </summary>
public class XpAward : FullAuditedEntity
{
    /// <summary>FK to <see cref="StudentXpProfile"/> within the gamification schema (cascade delete).</summary>
    public int StudentXpProfileId { get; set; }
    public StudentXpProfile StudentXpProfile { get; set; } = null!;

    /// <summary>
    /// EventId from the originating integration event. Combined with <see cref="Reason"/>,
    /// this forms the unique idempotency key (<c>UX_XpAwards_OriginEventId_Reason</c>).
    /// </summary>
    public Guid OriginEventId { get; set; }

    /// <summary>Reason for the award — stored as int. Drives idempotency + analytics.</summary>
    public XpReason Reason { get; set; }

    /// <summary>XP amount credited for this award (positive integer).</summary>
    public int XpAmount { get; set; }

    /// <summary>
    /// UTC time of the originating event (<c>OccurredOnUtc</c> from the integration event),
    /// NOT the wall-clock time of the DB insert. Preserves causality for the ledger.
    /// </summary>
    public DateTime OccurredAtUtc { get; set; }

    /// <summary>Soft reference to the originating lesson. No cross-module FK.</summary>
    public int? LessonId { get; set; }

    /// <summary>Soft reference to the originating skill. No cross-module FK.</summary>
    public int? SkillId { get; set; }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new XpAward ledger entry. No mutation after creation.
    ///
    /// Takes the <paramref name="profile"/> navigation reference instead of a raw FK int so
    /// that EF Core can resolve the real PK at SaveChanges time even when the profile was
    /// created in the same unit of work (i.e. <c>profile.Id == 0</c> before commit).
    /// This is the standard EF graph-navigation pattern for "insert parent + child together"
    /// (Option A.1 — see P4-02 fix-up notes).
    /// </summary>
    public static XpAward Create(
        StudentXpProfile profile,
        XpReason reason,
        int xpAmount,
        Guid originEventId,
        DateTime occurredAtUtc,
        int? lessonId = null,
        int? skillId = null)
        => new()
        {
            StudentXpProfile = profile,
            Reason = reason,
            XpAmount = xpAmount,
            OriginEventId = originEventId,
            OccurredAtUtc = occurredAtUtc,
            LessonId = lessonId,
            SkillId = skillId,
        };
}
