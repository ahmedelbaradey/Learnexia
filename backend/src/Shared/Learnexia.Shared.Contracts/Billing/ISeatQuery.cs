namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Cross-module seam: returns the active paid seat count and the child ids that hold those
/// seats for a given parent (P10-13-BE-8).
///
/// <para>Consumed by <c>IFamilyEnergyAllocationService</c> in Billing to compute the grant
/// denominator and decide which children receive allocation rows.</para>
///
/// <para>Implemented by a <strong>temporary in-Billing impl</strong> (P10-13) that resolves
/// active children via <c>IParentChildQuery.GetChildIdsForParentAsync</c> and returns
/// <c>IncludedSeats(tier) + 0 PurchasedExtraSeats</c> as the seat count — until P10-14
/// replaces this with the real seat-backed implementation (<c>P10-14-BE-3a</c> cutover).</para>
///
/// <para>No cross-module FK. <see cref="ActiveSeatInfo.ActiveChildIds"/> are loose int ids.</para>
/// </summary>
public interface ISeatQuery
{
    /// <summary>
    /// Returns the active paid seat count and the child ids that hold active seats for
    /// <paramref name="parentUserId"/>.
    /// </summary>
    Task<ActiveSeatInfo> GetActiveSeatsAsync(int parentUserId, CancellationToken ct = default);
}

/// <summary>
/// Result returned by <see cref="ISeatQuery.GetActiveSeatsAsync"/>.
/// </summary>
/// <param name="ActivePaidSeats">
/// Number of active paid seats for the parent (grant denominator for equal-split allocation).
/// </param>
/// <param name="ActiveChildIds">
/// Ids of children that hold active seats (the set to allocate subscription energy to).
/// </param>
public sealed record ActiveSeatInfo(
    int ActivePaidSeats,
    IReadOnlyList<int> ActiveChildIds);
