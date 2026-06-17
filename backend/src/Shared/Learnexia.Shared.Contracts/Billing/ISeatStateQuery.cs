namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Cross-module seam: answers "does this child currently hold an active seat?" (P10-15-BE-8).
///
/// <para>Consumed by <c>CreditSpendService</c> in Billing to gate AI energy spend — a child whose
/// seat is <c>NoSeatLocked</c> is denied spend with a localized message before any balance is touched.</para>
///
/// <para>No cross-module FK. <see cref="IsChildSeatActiveAsync"/> takes a plain <c>int childId</c>.</para>
///
/// <para><strong>Implement in Billing.Infrastructure</strong>; extend (not duplicate) the P10-14 seam.</para>
/// </summary>
public interface ISeatStateQuery
{
    /// <summary>
    /// Returns <see langword="true"/> if the child holds an <c>Active</c> seat
    /// (<c>SeatReservation.SeatState == SeatState.Active</c>); <see langword="false"/> if the child
    /// has no active reservation or the seat is <c>NoSeatLocked</c>.
    /// </summary>
    /// <param name="childId">The child user id (plain int — no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsChildSeatActiveAsync(int childId, CancellationToken ct = default);
}
