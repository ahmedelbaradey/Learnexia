namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Manages voluntary cycle-end seat removal scheduling (P10-15-BE-3).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, and scheduling logic lives in the Infrastructure implementation.
/// Handlers stay EF-free.</para>
///
/// <para><strong>Removal model (lead-approved 2026-06-17):</strong>
/// <list type="bullet">
///   <item>Voluntary parent-initiated seat removal (downgrade or cancel extra seat) schedules removal
///         at the NEXT cycle boundary — it does NOT touch <c>GraceEndsAt</c> (that is payment-failure only).</item>
///   <item><c>RemovalScheduledAt</c> is set on the <c>SeatReservation</c> row; the child keeps
///         <see cref="Enums.SeatState.Active"/> state until the enforcement job runs at that boundary.</item>
///   <item>No energy forfeit occurs; already-credited energy stays valid.</item>
/// </list>
/// </para>
/// </summary>
public interface ISeatSchedulingService
{
    /// <summary>
    /// Marks the <c>SeatReservation</c> for <paramref name="childId"/> under
    /// <paramref name="subscriptionId"/> with <c>RemovalScheduledAt = cycleEndUtc</c>.
    ///
    /// <para>Idempotent: if <c>RemovalScheduledAt</c> is already set to the same value, no-op.
    /// Does NOT move the seat to <see cref="Enums.SeatState.NoSeatLocked"/> — enforcement at
    /// renewal does that.</para>
    /// </summary>
    /// <param name="subscriptionId">Billing-internal subscription id.</param>
    /// <param name="childId">The child whose seat is to be removed at cycle end.</param>
    /// <param name="cycleEndUtc">The renewal boundary timestamp (<c>Subscription.CurrentCycleEnd</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task ScheduleRemovalAtCycleEndAsync(
        int subscriptionId,
        int childId,
        DateTime cycleEndUtc,
        CancellationToken ct = default);
}
