using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Append-only idempotency ledger for mission progress increments. One row per
/// (StudentMissionId, OriginEventId) — enforced by DB unique constraint
/// <c>UX_MissionProgressLogs_StudentMissionId_OriginEventId</c> (AC2 idempotency backstop).
///
/// A redelivered integration/domain event is detected by pre-checking this table;
/// the DB constraint is the backstop for concurrent races.
///
/// Derives from <see cref="CreationAuditedEntity"/> — strictly append-only, no mutation after insert.
/// Mirrors <see cref="HeartLoss"/> pattern.
///
/// FK to <see cref="StudentMission"/> is intra-module (gamification schema, cascade delete).
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public sealed class MissionProgressLog : CreationAuditedEntity
{
    /// <summary>FK to the <see cref="StudentMission"/> this increment applies to (cascade delete).</summary>
    public int StudentMissionId { get; private set; }

    /// <summary>Navigation property — resolved by EF at SaveChanges time.</summary>
    public StudentMission StudentMission { get; private set; } = null!;

    /// <summary>
    /// EventId from the originating integration/domain event.
    /// One row per (StudentMissionId, OriginEventId) — DB unique constraint backstop for idempotency.
    /// </summary>
    public Guid OriginEventId { get; private set; }

    /// <summary>
    /// Short discriminator for the triggering event type — e.g., "LessonCompletedIntegrationEvent",
    /// "StreakAdvancedDomainEvent". Max 60 chars.
    /// </summary>
    public string OriginEventType { get; private set; } = null!;

    /// <summary>
    /// The increment applied to <c>StudentMission.Progress</c> by this event.
    /// Usually 1; field allows future variable increments (e.g., AnswerSubmitted with CorrectAnswerCount > 1).
    /// </summary>
    public int ProgressDelta { get; private set; }

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private MissionProgressLog() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new idempotency log row. Takes the navigation reference to <paramref name="mission"/>
    /// so EF Core can resolve the real FK at SaveChanges time (mirrors <see cref="StudentBadge.Create"/>
    /// graph-navigation pattern).
    /// </summary>
    public static MissionProgressLog Create(
        StudentMission mission,
        Guid originEventId,
        string originEventType,
        int progressDelta)
        => new()
        {
            StudentMission = mission,
            OriginEventId = originEventId,
            OriginEventType = originEventType,
            ProgressDelta = progressDelta,
        };
}
