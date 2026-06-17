using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Contracts;

/// <summary>
/// REAL <see cref="ISeatQuery"/> implementation backed by <see cref="SeatReservation"/> rows
/// (P10-14-BE-3a — replaces the now-deleted TemporarySeatQuery).
///
/// <para>Returns the active paid seat count (formula: <c>IncludedSeats(plan) + PurchasedExtraSeats</c>
/// capped at <c>seats.max</c>) and the child ids holding <c>Active</c> reservations for the given parent.
/// Consumed by <c>IFamilyEnergyAllocationService</c> in the P10-13 grant job as the equal-split
/// denominator.</para>
///
/// <para>Loose int ids only — no cross-module FK.</para>
/// </summary>
public sealed class RealSeatQuery : ISeatQuery
{
    private readonly BillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;

    public RealSeatQuery(
        BillingDbContext db,
        IGlobalSettingsProvider settings,
        ILoggerManager logger)
    {
        _db       = db;
        _settings = settings;
        _logger   = logger;
    }

    /// <inheritdoc/>
    public async Task<ActiveSeatInfo> GetActiveSeatsAsync(int parentUserId, CancellationToken ct = default)
    {
        var maxSeats = _settings.GetInt(GlobalSettingKeys.SeatsMax, 5);

        // Resolve the billing-active subscription.
        var sub = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.ParentUserId == parentUserId
                     && (s.Status == SubscriptionStatus.Active
                         || s.Status == SubscriptionStatus.PastDue
                         || s.Status == SubscriptionStatus.Dunning))
            .FirstOrDefaultAsync(ct);

        // BLOCKER-1: null subscription = implicit Free plan (seats.included_free, 0 extra).
        // Free parents with no Subscription row still have their included seat entitlement.
        if (sub is null)
        {
            var freeIncluded = _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);
            var freePaidSeats = Math.Min(freeIncluded, maxSeats);
            _logger.LogInfo(
                $"RealSeatQuery: no billing-active subscription for parent={parentUserId} — treating as Free (includedSeats={freeIncluded}).");
            // No SeatReservation rows can exist without a subscription row, so activeChildIds = [].
            return new ActiveSeatInfo(freePaidSeats, []);
        }

        // Resolve included seats from the plan row.
        var plan = await _db.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == sub.PlanCode, ct);

        var includedSeats = plan?.IncludedSeats
            ?? _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);

        var activePaidSeats = Math.Min(includedSeats + sub.PurchasedExtraSeats, maxSeats);

        // Active child ids = children with an Active SeatReservation for this subscription,
        // ordered by reservation time (earliest-reserved = OQ-F tie-break).
        var activeChildIds = await _db.SeatReservations
            .AsNoTracking()
            .Where(r => r.SubscriptionId == sub.Id && r.Status == SeatStatus.Active)
            .OrderBy(r => r.ReservedAt)
            .Select(r => r.ChildId)
            .Take(activePaidSeats)          // cap at the paid count
            .ToListAsync(ct);

        _logger.LogInfo(
            $"RealSeatQuery: parent={parentUserId}, activePaidSeats={activePaidSeats}, " +
            $"activeChildCount={activeChildIds.Count}.");

        return new ActiveSeatInfo(
            ActivePaidSeats: activePaidSeats,
            ActiveChildIds : activeChildIds);
    }
}
