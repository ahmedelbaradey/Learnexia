namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Executes the seat enforcement pass for a subscription (P10-15-BE-4).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, and enforcement logic lives in the Infrastructure implementation.
/// Handlers stay EF-free.</para>
///
/// <para><strong>Enforcement model (lead-approved 2026-06-17):</strong>
/// <list type="bullet">
///   <item>Run at renewal OR when the grace window expires: reconcile seated children vs
///         <c>paidActiveSeats</c>.</item>
///   <item>If <c>occupiedSeats &gt; paidActiveSeats</c>, lock the EXCESS children using an
///         earliest-reserved tie-break (<c>SeatReservation.ReservedAt ASC</c> — oldest seats kept first).</item>
///   <item>Lock = set <c>SeatState = NoSeatLocked</c> + remove the child's
///         <c>ChildEnergyAllocation</c> row (no future entitlement).</item>
///   <item>NO mid-cycle energy forfeit: already-credited energy in the wallet is preserved.</item>
///   <item>Append a <c>SeatLedgerEntry{Locked}</c> for each locked child.</item>
///   <item>Also apply any <c>RemovalScheduledAt</c> seats that have passed.</item>
/// </list>
/// </para>
/// </summary>
public interface ISeatEnforcementService
{
    /// <summary>
    /// Runs the enforcement pass for <paramref name="subscriptionId"/>.
    ///
    /// <para>Idempotent: re-running on an already-enforced subscription is a no-op (state-based
    /// guard: checks current <c>SeatState</c> before locking).</para>
    /// </summary>
    /// <param name="subscriptionId">Billing-internal subscription id.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Number of children locked (0 if no enforcement was needed).</returns>
    Task<int> EnforceAsync(int subscriptionId, CancellationToken ct = default);
}
