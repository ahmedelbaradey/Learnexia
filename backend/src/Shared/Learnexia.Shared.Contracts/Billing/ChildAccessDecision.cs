namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Structured result of <see cref="IChildAccessStateQuery.GetAccessDecisionAsync"/>.
/// Provides the reason for a deny — enabling handlers to surface the correct localized message
/// rather than a generic "access denied" fallback (P10-18 security fix).
/// </summary>
public enum ChildAccessDecision
{
    /// <summary>
    /// The child holds an active seat AND has not been paused by their parent.
    /// AI access is permitted.
    /// </summary>
    Allowed = 1,

    /// <summary>
    /// The child's seat is not active (no reservation row, or seat in a non-Active/Reserved state,
    /// or SeatState is not Active). Corresponds to <see cref="DebitOutcome.SeatLocked"/>.
    /// Surface <see cref="Resources.SharedResourcesKey.ChildSeatLockedNoEnergy"/> to the student.
    /// </summary>
    SeatLocked = 2,

    /// <summary>
    /// The child has been paused by their parent (<c>ParentPauseState != Active</c>).
    /// The seat itself is active, but the parent has suspended AI access.
    /// Corresponds to <see cref="DebitOutcome.ParentPaused"/>.
    /// Surface <see cref="Resources.SharedResourcesKey.ChildAccessPausedByParent"/> to the student.
    /// </summary>
    Paused = 3,
}
