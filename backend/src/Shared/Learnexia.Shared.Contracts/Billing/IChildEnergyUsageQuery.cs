namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Cross-module read seam: Billing implements, Parent (and any future consumer) injects.
/// Returns per-child energy balance (remaining / allocated / spent) and a windowed usage
/// breakdown by helper kind.
///
/// <para>Registered in <c>AddBillingInfrastructure()</c> as Scoped.</para>
///
/// <para><strong>PURE READ — never writes.</strong> This seam explicitly does NOT call
/// <see cref="ICreditSpendService.GetBalanceAsync"/>, which bootstraps a
/// <c>ChildDailyUsage</c> row on first use (write-on-read side-effect). Instead it reads
/// <c>ChildEnergyAllocation</c> and <c>FamilyEnergyAccount</c> directly with
/// <c>AsNoTracking()</c>. A child with no wallet/allocation row returns zeroed fields —
/// not an error (AC3). Confirmed by the Execution Plan blocker note and verified by
/// security-auditor in Batch 4.</para>
///
/// <para>P5-08-BE-4 (Billing batch 1d).</para>
/// </summary>
public interface IChildEnergyUsageQuery
{
    /// <summary>
    /// Returns the current energy balance and recent usage for <paramref name="childId"/>.
    ///
    /// <para>Derives balance from the most-recent active <c>ChildEnergyAllocation</c> row and the
    /// family <c>FamilyEnergyAccount.PurchasedBalance</c>. Never creates or modifies any row.</para>
    /// </summary>
    /// <param name="childId">Loose int reference to the Identity child user (no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="ChildEnergySnapshot"/> — never null. Returns zeroed fields when the child
    /// has no allocation row (new child / no active subscription cycle).
    /// </returns>
    Task<ChildEnergySnapshot> GetSnapshotAsync(int childId, CancellationToken ct = default);

    /// <summary>
    /// Returns per-helper-kind energy usage counts for <paramref name="childId"/> over the UTC
    /// window [<paramref name="fromUtc"/>, <paramref name="toUtc"/>).
    ///
    /// <para>Derived from <c>CreditTransaction</c> rows where <c>Type = Spend</c> and the
    /// <c>ChildEnergyAllocationId</c> belongs to this child (or via the
    /// <c>FamilyEnergyAccount</c> fallback rows that carry the child allocation reference).
    /// Only AI-spend reason codes are counted (Hint / WhyWrong / DeepExplanation / PracticeGeneration).
    /// All four helper kinds are always present in the result (zero when unused).</para>
    ///
    /// <para>PURE READ — never writes.</para>
    /// </summary>
    /// <param name="childId">Loose int reference to the Identity child user (no cross-module FK).</param>
    /// <param name="fromUtc">Window start (inclusive), UTC.</param>
    /// <param name="toUtc">Window end (exclusive), UTC.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// Exactly 4 <see cref="EnergyUsageByKind"/> entries — never null.
    /// Zero counts when the child has no spend transactions in the window.
    /// </returns>
    Task<IReadOnlyList<EnergyUsageByKind>> GetUsageByKindAsync(
        int childId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default);
}

/// <summary>
/// Energy balance snapshot for a child, derived read-only from the billing wallet.
/// Produced by <see cref="IChildEnergyUsageQuery.GetSnapshotAsync"/>.
/// </summary>
/// <param name="Remaining">
/// Credits remaining in the child's current allocation cycle
/// (<c>ChildEnergyAllocation.Remaining</c> = <c>AllocatedAmount − SpentAmount</c>).
/// Zero when no active allocation.
/// </param>
/// <param name="Allocated">
/// Total credits allocated to the child for the current cycle
/// (<c>ChildEnergyAllocation.AllocatedAmount</c>). Zero when no active allocation.
/// </param>
/// <param name="Spent">
/// Credits spent so far this cycle (<c>ChildEnergyAllocation.SpentAmount</c>).
/// Zero when no active allocation.
/// </param>
/// <param name="PurchasedBalance">
/// Shared family purchased-pack balance (<c>FamilyEnergyAccount.PurchasedBalance</c>).
/// Zero when the family has no wallet.
/// </param>
/// <param name="CycleEndsAtUtc">
/// When the current allocation cycle expires; null when no active cycle.
/// </param>
public record ChildEnergySnapshot(
    int Remaining,
    int Allocated,
    int Spent,
    int PurchasedBalance,
    DateTime? CycleEndsAtUtc);

/// <summary>
/// Per-helper-kind energy usage count over a UTC window.
/// Produced by <see cref="IChildEnergyUsageQuery.GetUsageByKindAsync"/>.
/// </summary>
/// <param name="Kind">
/// The helper kind identifier. One of: <c>hints</c>, <c>explain</c>, <c>deep</c>, <c>practice</c>.
/// These literals match the FE contract (<c>EnergyUsageStub.kind</c>).
/// </param>
/// <param name="Count">Number of spend transactions of this kind in the window.</param>
public record EnergyUsageByKind(string Kind, int Count);
