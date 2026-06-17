namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Result of a seat reservation attempt returned by <see cref="ISubscriptionSeatContract.ReserveSeatAsync"/>
/// (P10-14-BE-4).
///
/// <para>Loose enum — carries no Billing-module types so the consuming Parent module
/// can reference only <c>Shared.Contracts</c>.</para>
/// </summary>
public enum SeatReservationResult
{
    /// <summary>A seat was successfully reserved.</summary>
    Reserved = 0,

    /// <summary>No free seats are available; the parent must buy extra seats before adding a child.</summary>
    NoFreeSeat = 1,

    /// <summary>The reservation is idempotent — a prior reservation for the same key already exists.</summary>
    AlreadyReserved = 2,
}
