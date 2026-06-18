namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Type of event recorded in the <see cref="Entities.SeatLedgerEntry"/> audit ledger (P10-15-BE-11).
///
/// <para>All seat lifecycle events — purchase, cancel scheduling, removal at renewal, enforcement lock,
/// parent-choice, and reactivation — are routed here. The energy ledger (<c>CreditTransaction</c>)
/// is NEVER used for seat events (lead decision 2026-06-17).</para>
/// </summary>
public enum SeatLedgerEventType
{
    /// <summary>A seat was purchased (extra-seat add-on confirmed via the P10-14 webhook).</summary>
    Purchased = 0,

    /// <summary>A voluntary extra-seat cancellation was scheduled at cycle end (P10-14-BE-8).</summary>
    CancelScheduled = 1,

    /// <summary>A scheduled seat removal was applied at the renewal boundary (P10-15-BE-4).</summary>
    RemovedAtRenewal = 2,

    /// <summary>A child's seat was moved to NoSeatLocked by the enforcement service (P10-15-BE-4).</summary>
    Locked = 3,

    /// <summary>A NoSeatLocked child was reactivated via the prorated checkout seam (P10-15-BE-7).</summary>
    Reactivated = 4,

    /// <summary>A parent applied a choose-active-children selection (P10-15-BE-6).</summary>
    ChoiceApplied = 5,

    // ── P10-18: Parent-pause audit entries ──────────────────────────────────────────

    /// <summary>A parent paused a child's AI access (P10-18-BE-3). No energy or seat writes.</summary>
    ParentPause = 6,

    /// <summary>A parent unpaused a child's AI access (P10-18-BE-3). No energy or seat writes.</summary>
    ParentUnpause = 7,
}
