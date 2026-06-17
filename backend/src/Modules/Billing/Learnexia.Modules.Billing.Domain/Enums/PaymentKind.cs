namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Describes the commercial purpose of a <see cref="Entities.Payment"/> record.
/// </summary>
public enum PaymentKind
{
    /// <summary>A recurring/one-time Premium subscription charge.</summary>
    Subscription = 0,

    /// <summary>A one-off energy-pack purchase for a specific child.</summary>
    Pack = 1,

    /// <summary>A monthly extra-seat add-on purchase (P10-14). Confirmed via the provider webhook only.</summary>
    Seat = 2,

    /// <summary>
    /// A prorated reactivation payment for a child whose seat is <c>NoSeatLocked</c> (P10-15-BE-7).
    /// On webhook confirmation, the seat transitions from <c>NoSeatLocked</c> back to <c>Active</c>
    /// and a <c>SeatLedgerEntry{Reactivated}</c> is appended. No energy is minted (money-only).
    /// </summary>
    SeatReactivation = 3,
}
