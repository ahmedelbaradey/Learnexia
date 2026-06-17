namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Cross-module seam: exposes seat reservation / release / free-seat-check to the Parent module
/// without coupling it to any <c>Billing.*</c> project (P10-14-BE-4).
///
/// <para>Implemented in <c>Billing.Infrastructure</c> (delegates to <c>ISeatService</c>).
/// Consumed by the Parent <c>AddChildCommandHandler</c>.</para>
///
/// <para><strong>Module isolation:</strong> all ids are loose <c>int</c>; no Billing types
/// are leaked across the seam. The Parent module must reference <em>only</em> this interface
/// and <see cref="SeatReservationResult"/> — never any <c>Billing.*</c> type.</para>
///
/// <para><strong>No cross-module FK:</strong> <c>parentUserId</c> / <c>childId</c> are the same
/// loose ints used throughout the system; Billing stores them without referential constraints.</para>
/// </summary>
public interface ISubscriptionSeatContract
{
    /// <summary>
    /// Atomically checks whether <paramref name="parentUserId"/> has a free seat and, if so,
    /// inserts a transient <c>Reserved</c> reservation for <paramref name="childId"/>.
    ///
    /// <para>Idempotent on <paramref name="idempotencyKey"/>: a retry returns the prior result
    /// without double-reserving.</para>
    ///
    /// <para>On <see cref="SeatReservationResult.NoFreeSeat"/> the caller must NOT create the
    /// child account and must return a user-facing "no free seat" error.</para>
    /// </summary>
    Task<SeatReservationResult> ReserveSeatAsync(
        int parentUserId,
        int childId,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// Compensation path: transitions the child's <c>Reserved</c> or <c>Active</c> seat back
    /// to <c>Released</c>. Idempotent — no-op if no reservation exists or already released.
    /// </summary>
    Task ReleaseSeatAsync(int parentUserId, int childId, CancellationToken ct = default);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="parentUserId"/> has at least one free seat
    /// (active paid seat count > current occupied count). Non-locking read — use
    /// <see cref="ReserveSeatAsync"/> for the atomic reserve-or-fail path.
    /// </summary>
    Task<bool> HasFreeSeatAsync(int parentUserId, CancellationToken ct = default);

    /// <summary>
    /// Transitions the child's seat from <c>Reserved</c> → <c>Active</c> (called after
    /// successful child creation + link). Idempotent — no-op if already <c>Active</c>.
    /// </summary>
    Task ActivateSeatAsync(int parentUserId, int childId, CancellationToken ct = default);
}
