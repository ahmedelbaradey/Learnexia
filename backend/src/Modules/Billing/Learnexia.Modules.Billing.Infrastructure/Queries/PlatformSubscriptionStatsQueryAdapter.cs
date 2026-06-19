using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Queries;

/// <summary>
/// Implements <see cref="IPlatformSubscriptionStatsQuery"/> by querying <see cref="BillingDbContext"/>
/// for platform-wide subscription statistics required by the P7-10 analytics dashboard.
///
/// <para>Cross-module isolation: the Identity façade injects <see cref="IPlatformSubscriptionStatsQuery"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Billing project
/// directly. This adapter lives in <c>Billing.Infrastructure</c> and is registered in
/// <c>AddBillingInfrastructure</c>.</para>
///
/// <para>Revenue: NOT included in v1. The payment provider is Fake/Paymob-later; revenue figures
/// would be synthetic. The <c>RevenueNaReason</c> field carries an explicit N/A marker so the
/// dashboard can render an honest "not available" card rather than a fake number.</para>
///
/// <para>Active subscriptions: rows where <c>Status = Active</c>.
/// PendingPayment, Dunning, Downgrading, etc. are not counted as "active subscriptions".</para>
///
/// <para>All results are sentinel-safe: returns zeroed stats when no subscriptions exist — never null, never throws.</para>
/// </summary>
public sealed class PlatformSubscriptionStatsQueryAdapter : IPlatformSubscriptionStatsQuery
{
    private readonly BillingDbContext _db;

    public PlatformSubscriptionStatsQueryAdapter(BillingDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<PlatformSubscriptionStats> GetPlatformAsync(CancellationToken ct = default)
    {
        // Count active subscriptions grouped by PlanCode.
        // SubscriptionStatus.Active = 0; PlanCode is stored as int.
        // AsNoTracking — read-only aggregate, no change tracking needed.
        var byPlan = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active)
            .GroupBy(s => s.PlanCode)
            .Select(g => new { PlanCode = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var tierCounts = byPlan
            .Select(b => new SubscriptionTierCount(
                PlanCode: b.PlanCode.ToString(),
                Count:    b.Count))
            .OrderBy(t => t.PlanCode)
            .ToList();

        int totalActive = byPlan.Sum(b => b.Count);

        // Revenue is explicitly N/A in v1 — Fake payment provider produces synthetic data.
        const string RevenueNa = "N/A (no live payments — Fake provider active; Paymob not yet integrated)";

        return new PlatformSubscriptionStats(
            TotalActiveSubscriptions: totalActive,
            ByTier:                   tierCounts,
            RevenueNaReason:          RevenueNa);
    }
}
