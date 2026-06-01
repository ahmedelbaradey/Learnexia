using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Append-only ledger row for every badge a student earns. One row per (StudentXpProfileId, BadgeDefinitionId) —
/// enforced by DB unique constraint <c>UX_StudentBadges_StudentXpProfileId_BadgeDefinitionId</c> (AC3 backstop).
///
/// Derives from <see cref="CreationAuditedEntity"/> (not <c>FullAuditedEntity</c>) because the ledger
/// is strictly append-only — soft-deletion columns must not be present, as their presence would allow
/// <c>HasBadgeAsync</c> to return false for a soft-deleted row and break the idempotency guarantee.
/// Mirrors <see cref="HeartLoss"/> choice (P4-04).
///
/// FK to <see cref="StudentXpProfile"/> is intra-module (gamification schema, cascade delete).
/// FK to <see cref="BadgeDefinition"/> is intra-module (gamification schema, RESTRICT delete — catalog
/// rows must not remove student achievement records; badge definitions are immutable once seeded).
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public sealed class StudentBadge : CreationAuditedEntity
{
    /// <summary>FK to <see cref="StudentXpProfile"/> within the gamification schema (cascade delete).</summary>
    public int StudentXpProfileId { get; private set; }

    /// <summary>Navigation property — resolved by EF at SaveChanges time.</summary>
    public StudentXpProfile StudentXpProfile { get; private set; } = null!;

    /// <summary>FK to <see cref="BadgeDefinition"/> within the gamification schema (restrict delete).</summary>
    public int BadgeDefinitionId { get; private set; }

    /// <summary>Navigation property — resolved by EF at SaveChanges time.</summary>
    public BadgeDefinition BadgeDefinition { get; private set; } = null!;

    /// <summary>
    /// EventId from the originating event. Used for analytics, XP bonus correlation, and
    /// traceability back to the integration / domain event that triggered the award.
    /// Fresh <c>Guid.NewGuid()</c> per command invocation (D9).
    /// </summary>
    public Guid OriginEventId { get; private set; }

    /// <summary>
    /// Short discriminator for the triggering event type — e.g., "LessonCompletedIntegrationEvent",
    /// "StreakAdvancedDomainEvent", "StudentLeveledUpDomainEvent". Max 60 chars.
    /// </summary>
    public string OriginEventType { get; private set; } = null!;

    /// <summary>
    /// UTC time of the originating event (<c>OccurredOnUtc</c> from the triggering event),
    /// NOT the wall-clock time of the DB insert. Preserves causality for the audit log.
    /// </summary>
    public DateTime AwardedAtUtc { get; private set; }

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private StudentBadge() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new StudentBadge ledger row. No mutation after creation.
    ///
    /// Takes <paramref name="profile"/> and <paramref name="definition"/> navigation references
    /// instead of raw FK ints so that EF Core can resolve the real PKs at SaveChanges time even
    /// when the profile was created in the same unit of work (mirrors <see cref="HeartLoss.Create"/>
    /// and <c>XpAward.Create</c> pattern — Option A.1 fix-up from P4-02).
    /// </summary>
    public static StudentBadge Create(
        StudentXpProfile profile,
        BadgeDefinition definition,
        Guid originEventId,
        string originEventType,
        DateTime awardedAtUtc)
        => new()
        {
            StudentXpProfile = profile,
            BadgeDefinition = definition,
            OriginEventId = originEventId,
            OriginEventType = originEventType,
            AwardedAtUtc = awardedAtUtc,
        };
}
