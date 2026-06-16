namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for the family subscription-grant allocation operation (P10-13-BE-5).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF, transaction, and equal-split ledger logic lives in the Infrastructure implementation.
/// Handlers stay EF-free.</para>
///
/// <para>Active-seat children come from <c>ISeatQuery</c> (Shared.Contracts) — loose int ids,
/// no cross-module FK.</para>
/// </summary>
public interface IFamilyEnergyAllocationService
{
    /// <summary>
    /// Deposits <paramref name="grantAmount"/> into the family wallet <c>SubscriptionBalance</c>
    /// and EQUAL-splits it across the active-seat children as per-child
    /// <c>ChildEnergyAllocation</c> rows.
    ///
    /// <para><strong>Equal-split with deterministic remainder:</strong>
    /// integer division; first-N children get +1 so the allocated sum == grant exactly.</para>
    ///
    /// <para><strong>Idempotent</strong> on <c>(parentUserId, cycleStart)</c>: a second call
    /// for the same family+cycle is a no-op (idempotency key uniqueness on the ledger).</para>
    ///
    /// <para>Runs in an explicit transaction (ACID). Old allocation rows from the prior cycle
    /// are expired (subscription balance zeroed) inside the same transaction.</para>
    /// </summary>
    /// <param name="parentUserId">Owning parent (wallet owner).</param>
    /// <param name="grantAmount">Total subscription energy to deposit and split.</param>
    /// <param name="cycleStart">Start of the billing cycle (UTC).</param>
    /// <param name="cycleEnd">End of the billing cycle (UTC) — stored on allocation rows.</param>
    /// <param name="idempotencyKey">Caller-supplied unique key for this grant+cycle.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<AllocationResult> AllocateSubscriptionGrantAsync(
        int parentUserId,
        int grantAmount,
        DateTime cycleStart,
        DateTime cycleEnd,
        string idempotencyKey,
        CancellationToken ct = default);
}

/// <summary>Result of <see cref="IFamilyEnergyAllocationService.AllocateSubscriptionGrantAsync"/>.</summary>
public sealed record AllocationResult
{
    public bool Succeeded    { get; init; }
    public bool WasDuplicate { get; init; }
    public int  ChildCount   { get; init; }
    public int  TotalAllocated { get; init; }

    public static AllocationResult Ok(int childCount, int totalAllocated)
        => new() { Succeeded = true, ChildCount = childCount, TotalAllocated = totalAllocated };

    public static AllocationResult Duplicate()
        => new() { Succeeded = true, WasDuplicate = true };

    public static AllocationResult NoChildren()
        => new() { Succeeded = true, ChildCount = 0, TotalAllocated = 0 };
}
