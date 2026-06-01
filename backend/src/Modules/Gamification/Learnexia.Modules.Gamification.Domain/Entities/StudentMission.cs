using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Per-period mission instance for a student. One row per (StudentXpProfileId, MissionDefinitionId, PeriodKey).
///
/// Derives from <see cref="CreationAuditedEntity"/> — the ledger is created once and then mutated
/// only by <c>IncrementMissionProgressCommandHandler</c> (handler-driven progress writes, not admin
/// edits). <see cref="Progress"/>, <see cref="Status"/>, and <see cref="CompletedAtUtc"/> are
/// <c>internal set</c> to allow mutation from the same assembly while preventing external writes.
///
/// Unique constraint <c>UX_StudentMissions_StudentXpProfileId_MissionDefinitionId_PeriodKey</c>
/// prevents duplicate instantiation under lazy-instantiation race conditions.
///
/// FK to <see cref="StudentXpProfile"/> is intra-module (gamification schema, cascade delete).
/// FK to <see cref="MissionDefinition"/> is intra-module (gamification schema, RESTRICT delete —
/// catalog rows must not cascade-delete student mission history; definitions are stable once seeded).
/// No cross-module FK constraints (module isolation rule 1).
/// </summary>
public sealed class StudentMission : CreationAuditedEntity
{
    /// <summary>FK to <see cref="StudentXpProfile"/> within the gamification schema (cascade delete).</summary>
    public int StudentXpProfileId { get; private set; }

    /// <summary>Navigation property — resolved by EF at SaveChanges time.</summary>
    public StudentXpProfile StudentXpProfile { get; private set; } = null!;

    /// <summary>FK to <see cref="MissionDefinition"/> within the gamification schema (restrict delete).</summary>
    public int MissionDefinitionId { get; private set; }

    /// <summary>Navigation property — resolved by EF at SaveChanges time.</summary>
    public MissionDefinition MissionDefinition { get; private set; } = null!;

    /// <summary>
    /// Denormalized cadence snapshot — copied from <see cref="MissionDefinition.Cadence"/> at
    /// instantiation time so the row can be filtered by type without a join.
    /// </summary>
    public MissionType MissionType { get; private set; }

    /// <summary>
    /// Stable period identifier — e.g. "D:2026-06-01" (daily) or "W:2026-22" (weekly ISO week).
    /// Used as the deduplication key for lazy instantiation race protection.
    /// </summary>
    public string PeriodKey { get; private set; } = null!;

    /// <summary>UTC start of the period. Used for dashboard display.</summary>
    public DateTime PeriodStartUtc { get; private set; }

    /// <summary>UTC end of the period — the expiry (AC1).</summary>
    public DateTime PeriodEndUtc { get; private set; }

    /// <summary>
    /// Denormalized target snapshot — copied from <see cref="MissionDefinition.Target"/> at
    /// instantiation time so catalog edits do not affect in-flight missions.
    /// </summary>
    public int Target { get; private set; }

    /// <summary>Current progress towards <see cref="Target"/>. Default 0. Mutated by handler.</summary>
    public int Progress { get; internal set; }

    /// <summary>Mission lifecycle state. Default NotStarted. Mutated by handler.</summary>
    public MissionStatus Status { get; internal set; }

    /// <summary>Set when <see cref="Status"/> transitions to <see cref="MissionStatus.Completed"/>. Nullable.</summary>
    public DateTime? CompletedAtUtc { get; internal set; }

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private StudentMission() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="StudentMission"/> instance for a student's current period.
    /// Copies <c>MissionType</c> and <c>Target</c> from <paramref name="definition"/> as
    /// denormalized snapshots (catalog edit isolation).
    /// </summary>
    public static StudentMission Create(
        StudentXpProfile profile,
        MissionDefinition definition,
        string periodKey,
        DateTime periodStartUtc,
        DateTime periodEndUtc)
        => new()
        {
            StudentXpProfile = profile,
            MissionDefinition = definition,
            MissionType = definition.Cadence,
            PeriodKey = periodKey,
            PeriodStartUtc = periodStartUtc,
            PeriodEndUtc = periodEndUtc,
            Target = definition.Target,
            Progress = 0,
            Status = MissionStatus.NotStarted,
        };

    // ---------------------------------------------------------------------------
    // Progress mutation (called by IncrementMissionProgressCommandHandler via MissionProgressUpdater)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Applies an increment to this mission's progress. Clamps <see cref="Progress"/> to
    /// <see cref="Target"/>. Sets <paramref name="completedNow"/> = true when the completion
    /// threshold is crossed for the first time. No-op if already <see cref="MissionStatus.Completed"/>
    /// or <see cref="MissionStatus.Expired"/>.
    /// </summary>
    public void ApplyProgress(int incrementBy, DateTime occurredAtUtc, out bool completedNow)
    {
        completedNow = false;

        if (Status is MissionStatus.Completed or MissionStatus.Expired)
            return;

        var newProgress = Math.Min(Target, Progress + incrementBy);
        var crossedThreshold = newProgress >= Target && Status != MissionStatus.Completed;

        Progress = newProgress;

        if (Status == MissionStatus.NotStarted && newProgress > 0)
            Status = MissionStatus.InProgress;

        if (crossedThreshold)
        {
            Status = MissionStatus.Completed;
            CompletedAtUtc = occurredAtUtc;
            completedNow = true;
        }
    }
}
