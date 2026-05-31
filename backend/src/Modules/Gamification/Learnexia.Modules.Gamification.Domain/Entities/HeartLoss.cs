using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Append-only audit row for every heart-loss event. One row per discrete wrong-answer event.
/// Idempotency is enforced via a DB unique constraint on <c>UX_HeartLosses_OriginEventId</c>
/// — a redelivered integration event with the same <see cref="OriginEventId"/> will be
/// rejected at the database layer.
///
/// Derives from <see cref="CreationAuditedEntity"/> (not <c>FullAuditedEntity</c>) because the
/// ledger is strictly append-only — soft-deletion columns (<c>IsDeleted</c>/<c>DeletedAt</c>/
/// <c>DeletedBy</c>) must not be present. Their presence would break the idempotency guarantee:
/// <c>HasHeartLossAsync</c> returning false for a soft-deleted row with a matching
/// <see cref="OriginEventId"/> would allow the event to be double-processed.
///
/// FK to <see cref="StudentXpProfile"/> is intra-module (gamification schema, cascade delete).
/// No cross-module FK constraints (module isolation rule 1).
/// No mutation methods — the ledger is strictly append-only.
/// </summary>
public sealed class HeartLoss : CreationAuditedEntity
{
    /// <summary>FK to <see cref="StudentXpProfile"/> within the gamification schema (cascade delete).</summary>
    public int StudentXpProfileId { get; private set; }

    /// <summary>Navigation property — resolved by EF at SaveChanges time.</summary>
    public StudentXpProfile StudentXpProfile { get; private set; } = null!;

    /// <summary>
    /// EventId from the originating <c>AnswerSubmittedIntegrationEvent</c>.
    /// Unique — forms the idempotency key (<c>UX_HeartLosses_OriginEventId</c>).
    /// </summary>
    public Guid OriginEventId { get; private set; }

    /// <summary>Hearts count AFTER this loss (snapshot for analytics/debug).</summary>
    public int HeartsAfter { get; private set; }

    /// <summary>
    /// UTC time of the originating event (<c>OccurredOnUtc</c> from the integration event),
    /// NOT the wall-clock time of the DB insert. Preserves causality for the audit log.
    /// </summary>
    public DateTime OccurredAtUtc { get; private set; }

    /// <summary>
    /// Soft reference to the originating lesson (from <c>AnswerSubmittedIntegrationEvent.LessonId</c>).
    /// Nullable int — no cross-module FK constraint (module isolation rule 1).
    /// Mirrors <see cref="XpAward.LessonId"/> pattern.
    /// </summary>
    public int? LessonId { get; private set; }

    /// <summary>
    /// Soft reference to the originating skill (from <c>AnswerSubmittedIntegrationEvent.SkillId</c>).
    /// Nullable int — no cross-module FK constraint (module isolation rule 1).
    /// Mirrors <see cref="XpAward.SkillId"/> pattern.
    /// </summary>
    public int? SkillId { get; private set; }

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private HeartLoss() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new HeartLoss audit row. No mutation after creation.
    ///
    /// Takes the <paramref name="profile"/> navigation reference instead of a raw FK int so
    /// that EF Core can resolve the real PK at SaveChanges time even when the profile was
    /// created in the same unit of work (i.e. <c>profile.Id == 0</c> before commit).
    /// Mirrors the XpAward.Create pattern (P4-02 fix-up — Option A.1).
    /// </summary>
    public static HeartLoss Create(
        StudentXpProfile profile,
        Guid originEventId,
        int heartsAfter,
        DateTime occurredAtUtc,
        int? lessonId = null,
        int? skillId = null)
        => new()
        {
            StudentXpProfile = profile,
            OriginEventId = originEventId,
            HeartsAfter = heartsAfter,
            OccurredAtUtc = occurredAtUtc,
            LessonId = lessonId,
            SkillId = skillId,
        };
}
