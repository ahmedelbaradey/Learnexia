namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.SeatReservation"/> row (P10-14-BE-1).
///
/// <para>The unique filtered index on <c>(SubscriptionId, ChildId) WHERE Status IN (Reserved, Active)</c>
/// guarantees no double-occupancy for the same child within the same subscription.</para>
/// </summary>
public enum SeatStatus
{
    /// <summary>
    /// Seat reserved transiently during <c>AddChildCommandHandler</c>: the parent had a free seat
    /// and the reservation was recorded before child account creation began. Transitions to
    /// <see cref="Active"/> on successful child creation + link, or to <see cref="Released"/>
    /// on compensating rollback.
    /// </summary>
    Reserved = 0,

    /// <summary>
    /// Child account is created and linked. The seat is actively minting energy for this child
    /// (drives the P10-13 grant denominator via <c>ISeatQuery</c>).
    /// </summary>
    Active = 1,

    /// <summary>
    /// Seat has been released (compensation path — child creation/link failed after a
    /// <see cref="Reserved"/> was written, or an explicit seat cancel).
    /// Does NOT count against the occupancy ceiling.
    /// </summary>
    Released = 2,

    /// <summary>
    /// Child exists but has no active seat (e.g. enforcement after grace expiry — P10-15).
    /// Stored for lifecycle audit; the child cannot spend AI energy in this state.
    /// Does NOT count against the occupancy ceiling.
    /// </summary>
    NoSeat = 3,
}
