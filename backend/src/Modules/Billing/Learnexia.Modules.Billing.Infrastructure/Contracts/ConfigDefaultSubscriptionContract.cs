using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Contracts;

/// <summary>
/// Stub implementation of <see cref="IBillingSubscriptionContract"/> used until P10-05
/// (Subscription entity + plan management) lands.
///
/// <para>Returns all child ids from existing <c>CreditAccount</c> rows as <see cref="PlanTier.Free"/>.
/// New children (no account yet) are picked up on their first grant via the ledger flow.
/// When P10-05 ships, replace this registration with a real impl backed by the <c>Subscription</c>
/// entity — no change to <c>BillingGrantJob</c> required.</para>
/// </summary>
public sealed class ConfigDefaultSubscriptionContract : IBillingSubscriptionContract
{
    private readonly IBillingDbContext _db;

    public ConfigDefaultSubscriptionContract(IBillingDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ActiveChildPlanDto>> GetActiveChildrenWithTierAsync(CancellationToken ct = default)
    {
        // Return all children with existing accounts as Free tier.
        // Children without accounts receive their first account on the GrantCredit path (see GrantCreditCommandHandler).
        var childIds = await _db.CreditAccounts
            .AsNoTracking()
            .Select(a => new { a.ChildId, a.ChildTimeZoneId })
            .ToListAsync(ct);

        return childIds
            .Select(c => new ActiveChildPlanDto(c.ChildId, PlanTier.Free, c.ChildTimeZoneId))
            .ToList()
            .AsReadOnly();
    }
}
