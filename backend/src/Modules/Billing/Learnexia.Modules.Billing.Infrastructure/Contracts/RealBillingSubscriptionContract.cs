using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Contracts;

/// <summary>
/// Real implementation of <see cref="IBillingSubscriptionContract"/> — replaces the
/// old stub (which read from <c>billing.CreditAccounts</c>, now retired).
///
/// <para><strong>Tier resolution:</strong> a child's tier = their FAMILY's current plan tier.
/// <list type="bullet">
///   <item>Look up the child's parent via <see cref="IParentChildQuery.FindParentForChildAsync"/>.</item>
///   <item>Look up the parent's active subscription in <c>billing.Subscriptions</c>.</item>
///   <item>If the parent has an Active Premium subscription → <see cref="PlanTier.Premium"/>.</item>
///   <item>Otherwise (Free, PendingPayment, Downgrading, Canceled, or no row) → <see cref="PlanTier.Free"/>.</item>
/// </list>
/// </para>
///
/// <para><strong>CreditAccount RETIRED:</strong> Active children are now resolved via
/// <see cref="IParentChildQuery"/> (the cross-module <c>Shared.Contracts</c> seam) rather than
/// reading the retired <c>billing.CreditAccounts</c> table. This ensures the grant job picks up
/// ALL family children, including those who have never yet received a grant allocation row.</para>
///
/// <para><strong>Module isolation (rule 1):</strong> parent/child link data is NOT stored in the
/// Billing module. We consume it via <see cref="IParentChildQuery"/>. No cross-module FK.</para>
/// </summary>
public sealed class RealBillingSubscriptionContract : IBillingSubscriptionContract
{
    private readonly BillingDbContext _db;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly ILoggerManager _logger;

    public RealBillingSubscriptionContract(
        BillingDbContext db,
        IParentChildQuery parentChildQuery,
        ILoggerManager logger)
    {
        _db = db;
        _parentChildQuery = parentChildQuery;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActiveChildPlanDto>> GetActiveChildrenWithTierAsync(
        CancellationToken ct = default)
    {
        // 1. Load all Active Premium parent user ids from Subscriptions (one query).
        var premiumParentIds = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active && s.PlanCode == PlanCode.Premium)
            .Select(s => s.ParentUserId)
            .ToListAsync(ct);

        var premiumSet = new HashSet<int>(premiumParentIds);

        // 2. Collect known family wallets (provisioned families).
        //    Each FamilyEnergyAccount.ParentUserId maps to a parent whose children may be active.
        //    We also include Premium-subscribed parents who may not yet have a wallet.
        var walletParentIds = await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .Select(w => w.ParentUserId)
            .ToListAsync(ct);

        // Union: wallet parents + Premium-subscribed parents (they'll get a wallet on their first grant).
        var allParentIds = walletParentIds.Union(premiumParentIds).ToList();

        // 3. For each parent resolve their children → build the result list.
        var result = new List<ActiveChildPlanDto>();

        foreach (var parentId in allParentIds)
        {
            try
            {
                var childIds = await _parentChildQuery.GetChildIdsForParentAsync(parentId, ct);
                var tier = premiumSet.Contains(parentId) ? PlanTier.Premium : PlanTier.Free;

                foreach (var childId in childIds)
                    result.Add(new ActiveChildPlanDto(childId, tier, "Africa/Cairo"));
            }
            catch (Exception ex)
            {
                // Fail-soft per parent: log and skip so the grant job continues for other families.
                _logger.LogError(ex, $"RealBillingSubscriptionContract: failed to resolve children for parentId={parentId}. Skipping.");
            }
        }

        return result.AsReadOnly();
    }
}
