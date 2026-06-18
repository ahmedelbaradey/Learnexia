using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Queries;

/// <summary>
/// Postgres implementation of <see cref="IChildEnergyUsageQuery"/>.
///
/// <para><strong>PURE READ — no writes.</strong>
/// Does NOT call <see cref="CreditSpendService.GetBalanceAsync"/> (which bootstraps a
/// <c>ChildDailyUsage</c> row on first use — write-on-read). Instead reads
/// <c>ChildEnergyAllocation</c> and <c>FamilyEnergyAccount</c> directly with
/// <c>AsNoTracking()</c>. A child with no allocation row returns zeroed
/// <see cref="ChildEnergySnapshot"/> fields — not an error (AC3 / P5-08-BE-4 pure-read
/// contract). security-auditor verifies this in Batch 4.</para>
/// </summary>
public sealed class PostgresChildEnergyUsageQuery : IChildEnergyUsageQuery
{
    private readonly BillingDbContext _db;

    public PostgresChildEnergyUsageQuery(BillingDbContext db)
    {
        _db = db;
    }

    // ── GetSnapshotAsync ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<ChildEnergySnapshot> GetSnapshotAsync(
        int childId,
        CancellationToken ct = default)
    {
        // Find the child's most-recent allocation row (latest cycle start).
        var allocation = await _db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc)
            .Select(a => new
            {
                a.AllocatedAmount,
                a.SpentAmount,
                a.CycleEndUtc,
                a.FamilyEnergyAccountId,
            })
            .FirstOrDefaultAsync(ct);

        if (allocation is null)
        {
            // New child / no active subscription cycle — return zeroed snapshot (AC3).
            return new ChildEnergySnapshot(
                Remaining: 0,
                Allocated: 0,
                Spent: 0,
                PurchasedBalance: 0,
                CycleEndsAtUtc: null);
        }

        // Derive remaining (never negative — mirrors ChildEnergyAllocation.Remaining computed prop).
        var remaining = Math.Max(0, allocation.AllocatedAmount - allocation.SpentAmount);

        // Look up the shared purchased balance from the family wallet (read-only, no tracking).
        var purchasedBalance = 0;
        var wallet = await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .Where(w => w.Id == allocation.FamilyEnergyAccountId)
            .Select(w => new { w.PurchasedBalance })
            .FirstOrDefaultAsync(ct);
        if (wallet is not null)
            purchasedBalance = wallet.PurchasedBalance;

        return new ChildEnergySnapshot(
            Remaining: remaining,
            Allocated: allocation.AllocatedAmount,
            Spent: allocation.SpentAmount,
            PurchasedBalance: purchasedBalance,
            CycleEndsAtUtc: allocation.CycleEndUtc);
    }

    // ── GetUsageByKindAsync ───────────────────────────────────────────────────────

    // Reason codes that correspond to AI helper kinds (the ones the parent dashboard tracks).
    private static readonly HashSet<CreditReasonCode> AiSpendReasonCodes =
    [
        CreditReasonCode.AiHint,
        CreditReasonCode.AiWhyWrong,
        CreditReasonCode.AiDeepExplanation,
        CreditReasonCode.AiPracticeGeneration,
        // P10-13 wallet-model allocation-first codes for AI spend
        CreditReasonCode.SpendAllocation,
        CreditReasonCode.SpendPurchasedFallback,
    ];

    /// <inheritdoc/>
    public async Task<IReadOnlyList<EnergyUsageByKind>> GetUsageByKindAsync(
        int childId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        // Find the child's allocation IDs across all cycles (needed to filter ledger rows
        // to this child's allocation-first spend rows). Read-only.
        var allocationIds = await _db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.ChildId == childId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        // Initialise counters — all four kinds always present (zero when unused).
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["hints"]    = 0,
            ["explain"]  = 0,
            ["deep"]     = 0,
            ["practice"] = 0,
        };

        if (allocationIds.Count == 0)
            return BuildResult(counts);

        // Pull Spend transactions for this child's allocation rows in the window.
        // We scope to the child's ChildEnergyAllocationId to avoid counting unrelated rows.
        var spendRows = await _db.CreditTransactions
            .AsNoTracking()
            .Where(t => t.Type == CreditTransactionType.Spend
                     && t.ChildEnergyAllocationId != null
                     && allocationIds.Contains(t.ChildEnergyAllocationId!.Value)
                     && t.OccurredAtUtc >= fromUtc
                     && t.OccurredAtUtc < toUtc)
            .Select(t => t.ReasonCode)
            .ToListAsync(ct);

        // Map reason codes → helper kind counters.
        foreach (var code in spendRows)
        {
            var kind = MapReasonCodeToKind(code);
            if (kind is not null && counts.ContainsKey(kind))
                counts[kind]++;
        }

        return BuildResult(counts);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    private static string? MapReasonCodeToKind(CreditReasonCode code) => code switch
    {
        CreditReasonCode.AiHint               => "hints",
        CreditReasonCode.AiWhyWrong           => "explain",
        CreditReasonCode.AiDeepExplanation    => "deep",
        CreditReasonCode.AiPracticeGeneration => "practice",
        // SpendAllocation / SpendPurchasedFallback are container codes — they wrap the AI reason
        // via their companion rows, so we skip them here to avoid double-counting.
        _                                     => null,
    };

    private static IReadOnlyList<EnergyUsageByKind> BuildResult(Dictionary<string, int> counts) =>
    [
        new EnergyUsageByKind("hints",    counts["hints"]),
        new EnergyUsageByKind("explain",  counts["explain"]),
        new EnergyUsageByKind("deep",     counts["deep"]),
        new EnergyUsageByKind("practice", counts["practice"]),
    ];
}
