using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Gamification.Domain.Entities;

/// <summary>
/// Per-student participation record for a timed XP event. One row per (StudentXpProfileId, TimedEventId).
///
/// Derives from <see cref="CreationAuditedEntity"/> — mirrors <see cref="StudentMission"/> exactly:
/// the row is created once (lazily, on a student's first qualifying action inside the event window)
/// and then mutated only via <see cref="ApplyProgress"/>. <see cref="Progress"/>,
/// <see cref="Status"/>, and <see cref="CompletedUtc"/> are <c>internal set</c> to allow mutation
/// from within the Gamification assembly while preventing external writes.
///
/// Unique constraint <c>UX_TimedEventParticipations_TimedEventId_StudentXpProfileId</c> prevents
/// duplicate instantiation under lazy-instantiation race conditions (mirror
/// <c>UX_StudentMissions_…</c> — caller catches <c>DbUpdateException</c> and re-reads).
///
/// FK to <see cref="StudentXpProfile"/> — intra-module (gamification schema), CASCADE delete:
///   removing a profile removes all its participation history.
/// FK to <see cref="TimedEvent"/> — intra-module (gamification schema), RESTRICT delete:
///   catalog rows must not cascade-delete participation history; events are stable once activated.
/// No cross-module FK constraints (module isolation rule 1).
///
/// <see cref="EventEndUtc"/> is a denormalized snapshot of <see cref="TimedEvent.EndUtc"/> at
/// creation time so the ending-soon scan can filter by deadline without joining <c>TimedEvents</c>
/// (mirrors <see cref="StudentMission.PeriodEndUtc"/>).
/// </summary>
public sealed class TimedEventParticipation : CreationAuditedEntity
{
    /// <summary>FK to <see cref="TimedEvent"/> within the gamification schema (RESTRICT delete).</summary>
    public int TimedEventId { get; private set; }

    /// <summary>FK to <see cref="StudentXpProfile"/> within the gamification schema (CASCADE delete).</summary>
    public int StudentXpProfileId { get; private set; }

    /// <summary>Navigation property — resolved by EF at SaveChanges time.</summary>
    public StudentXpProfile StudentXpProfile { get; private set; } = null!;

    /// <summary>
    /// Denormalized target snapshot — copied from the resolved <c>TimedEvent.ParticipationTarget</c>
    /// (or <c>TimedEventParticipationOptions.DefaultTarget</c> when null) at creation time so a
    /// mid-event admin edit does not move the goalposts for in-flight participants.
    /// </summary>
    public int Target { get; private set; }

    /// <summary>
    /// Count of qualifying in-window XP actions accrued so far. Default 0. Clamped at
    /// <see cref="Target"/>. Mutated only by <see cref="ApplyProgress"/>.
    /// </summary>
    public int Progress { get; internal set; }

    /// <summary>
    /// Participation lifecycle state. Default <see cref="TimedEventParticipationStatus.NotStarted"/>.
    /// Mutated only by <see cref="ApplyProgress"/>.
    /// </summary>
    public TimedEventParticipationStatus Status { get; internal set; }

    /// <summary>UTC timestamp when this participation row was created (student's first qualifying action).</summary>
    public DateTime JoinedUtc { get; private set; }

    /// <summary>
    /// Set once when <see cref="Status"/> transitions to
    /// <see cref="TimedEventParticipationStatus.Completed"/>. Nullable.
    /// </summary>
    public DateTime? CompletedUtc { get; internal set; }

    /// <summary>
    /// Denormalized snapshot of <see cref="TimedEvent.EndUtc"/> at creation time.
    /// Used by the ending-soon scan to filter near-deadline in-progress participations
    /// without joining <c>TimedEvents</c> (mirrors <see cref="StudentMission.PeriodEndUtc"/>).
    /// </summary>
    public DateTime EventEndUtc { get; private set; }

    // ---------------------------------------------------------------------------
    // EF constructor
    // ---------------------------------------------------------------------------

    private TimedEventParticipation() { }

    // ---------------------------------------------------------------------------
    // Factory
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Creates a new <see cref="TimedEventParticipation"/> for a student's first qualifying action
    /// inside a timed event window. Snapshots <paramref name="target"/> and
    /// <paramref name="eventEndUtc"/> from the caller so mid-event catalog edits do not affect
    /// this in-flight participation.
    /// </summary>
    /// <param name="profile">The student's XP profile (intra-module FK source).</param>
    /// <param name="timedEventId">PK of the <see cref="TimedEvent"/> being joined.</param>
    /// <param name="target">
    ///   Resolved target — <c>TimedEvent.ParticipationTarget ?? TimedEventParticipationOptions.DefaultTarget</c>.
    /// </param>
    /// <param name="eventEndUtc">Snapshot of <c>TimedEvent.EndUtc</c> for the ending-soon scan.</param>
    /// <param name="joinedUtc">UTC timestamp of the first qualifying action.</param>
    public static TimedEventParticipation Create(
        StudentXpProfile profile,
        int timedEventId,
        int target,
        DateTime eventEndUtc,
        DateTime joinedUtc)
        => new()
        {
            StudentXpProfile  = profile,
            TimedEventId      = timedEventId,
            Target            = target,
            EventEndUtc       = eventEndUtc,
            JoinedUtc         = joinedUtc,
            Progress          = 0,
            Status            = TimedEventParticipationStatus.NotStarted,
        };

    // ---------------------------------------------------------------------------
    // Progress mutation (called by XpService via the accrual hook)
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Applies an increment to this participation's progress. Clamps <see cref="Progress"/> to
    /// <see cref="Target"/>. Sets <paramref name="completedNow"/> = <c>true</c> when the completion
    /// threshold is crossed for the first time.
    ///
    /// No-op if already <see cref="TimedEventParticipationStatus.Completed"/> (single-completion latch).
    /// Transitions <see cref="TimedEventParticipationStatus.NotStarted"/> → <see cref="TimedEventParticipationStatus.InProgress"/>
    /// on first progress, mirroring <see cref="StudentMission.ApplyProgress"/>.
    ///
    /// Window discipline (AC3) is enforced by the caller (XpService) checking
    /// <c>occurredAtUtc &lt; EventEndUtc</c> before invoking this method.
    /// </summary>
    /// <param name="incrementBy">Positive increment (typically 1 per qualifying action).</param>
    /// <param name="occurredAtUtc">UTC timestamp of the qualifying action; stored as <see cref="CompletedUtc"/> on first completion.</param>
    /// <param name="completedNow"><c>true</c> if this call crossed the completion threshold for the first time.</param>
    public void ApplyProgress(int incrementBy, DateTime occurredAtUtc, out bool completedNow)
    {
        completedNow = false;

        if (Status == TimedEventParticipationStatus.Completed)
            return;

        var newProgress = Math.Min(Target, Progress + incrementBy);
        var crossedThreshold = newProgress >= Target && Status != TimedEventParticipationStatus.Completed;

        Progress = newProgress;

        if (Status == TimedEventParticipationStatus.NotStarted && newProgress > 0)
            Status = TimedEventParticipationStatus.InProgress;

        if (crossedThreshold)
        {
            Status       = TimedEventParticipationStatus.Completed;
            CompletedUtc = occurredAtUtc;
            completedNow = true;
        }
    }
}
