using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Contracts;

/// <summary>
/// Real implementation of <see cref="IBillingSubscriptionContract"/> — replaces
/// <see cref="ConfigDefaultSubscriptionContract"/> when P10-05 lands.
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
/// <para><strong>Module isolation (rule 1):</strong> parent/child link data is NOT stored in the
/// Billing module. We consume it via <see cref="IParentChildQuery"/> (a <c>Shared.Contracts</c>
/// seam implemented by the Parent module). No cross-module FK.</para>
///
/// <para><strong>Source of active children:</strong> We start from <c>billing.CreditAccounts</c>
/// (the same set the stub used) — every child that has ever received a grant has a row. New children
/// without a row receive their first account on the next grant run, which is correct behaviour.</para>
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
        // 1. Load all known children from CreditAccounts (billing schema).
        var children = await _db.CreditAccounts
            .AsNoTracking()
            .Select(a => new { a.ChildId, a.ChildTimeZoneId })
            .ToListAsync(ct);

        // 2. Load all Active Premium parent user ids from Subscriptions (one query).
        //    We load all Premium parent ids into a HashSet for O(1) lookups.
        var premiumParentIds = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.Status == SubscriptionStatus.Active && s.PlanCode == PlanCode.Premium)
            .Select(s => s.ParentUserId)
            .ToListAsync(ct);

        var premiumSet = new HashSet<int>(premiumParentIds);

        // 3. For each child resolve their parent → look up tier.
        var result = new List<ActiveChildPlanDto>(children.Count);

        foreach (var child in children)
        {
            try
            {
                int? parentId = await _parentChildQuery.FindParentForChildAsync(child.ChildId, ct);

                var tier = parentId.HasValue && premiumSet.Contains(parentId.Value)
                    ? PlanTier.Premium
                    : PlanTier.Free;

                result.Add(new ActiveChildPlanDto(child.ChildId, tier, child.ChildTimeZoneId));
            }
            catch (Exception ex)
            {
                // Fail-soft per child: log and default to Free so the grant job continues.
                _logger.LogError(ex, $"RealBillingSubscriptionContract: failed to resolve tier for childId={child.ChildId}. Defaulting to Free.");
                result.Add(new ActiveChildPlanDto(child.ChildId, PlanTier.Free, child.ChildTimeZoneId));
            }
        }

        return result.AsReadOnly();
    }
}
