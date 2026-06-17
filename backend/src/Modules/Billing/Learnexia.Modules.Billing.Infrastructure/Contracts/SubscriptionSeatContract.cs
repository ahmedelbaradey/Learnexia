using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Contracts;

/// <summary>
/// Billing.Infrastructure implementation of <see cref="ISubscriptionSeatContract"/> (P10-14-BE-4).
///
/// <para>Delegates all business logic to the injected <see cref="ISeatService"/>. The contract
/// adapts the service's internal result types into the <c>Shared.Contracts</c> enum so no
/// Billing types bleed across the module boundary.</para>
///
/// <para>Registered as Scoped in <c>Billing.Infrastructure.DependencyInjection</c>; consumed by
/// the Parent module via the <c>Shared.Contracts</c> seam only.</para>
/// </summary>
public sealed class SubscriptionSeatContract : ISubscriptionSeatContract
{
    private readonly ISeatService _seatService;
    private readonly BillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ILoggerManager _logger;

    public SubscriptionSeatContract(
        ISeatService seatService,
        BillingDbContext db,
        IGlobalSettingsProvider settings,
        ILoggerManager logger)
    {
        _seatService = seatService;
        _db          = db;
        _settings    = settings;
        _logger      = logger;
    }

    public async Task<SeatReservationResult> ReserveSeatAsync(
        int parentUserId,
        int childId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var result = await _seatService.ReserveSeatAsync(parentUserId, childId, idempotencyKey, ct);

        return result.Outcome switch
        {
            SeatReservationOutcome.Reserved        => SeatReservationResult.Reserved,
            SeatReservationOutcome.NoFreeSeat      => SeatReservationResult.NoFreeSeat,
            SeatReservationOutcome.AlreadyReserved => SeatReservationResult.AlreadyReserved,
            _                                      => SeatReservationResult.NoFreeSeat,
        };
    }

    public Task ReleaseSeatAsync(int parentUserId, int childId, CancellationToken ct = default,
        string? reservationKey = null)
        => _seatService.ReleaseSeatAsync(parentUserId, childId, ct, reservationKey);

    public async Task<bool> HasFreeSeatAsync(int parentUserId, CancellationToken ct = default)
    {
        var maxSeats = _settings.GetInt(GlobalSettingKeys.SeatsMax, 5);

        var sub = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.ParentUserId == parentUserId
                     && (s.Status == SubscriptionStatus.Active
                         || s.Status == SubscriptionStatus.PastDue
                         || s.Status == SubscriptionStatus.Dunning))
            .FirstOrDefaultAsync(ct);

        // BLOCKER-1: null subscription = implicit Free plan (seats.included_free, 0 extra).
        // A Free parent with no subscription row is treated as having 1 available seat.
        if (sub is null)
        {
            var freeIncluded = _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);
            return freeIncluded > 0;
        }

        var plan = await _db.Plans.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == sub.PlanCode, ct);

        var includedSeats = plan?.IncludedSeats
            ?? _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);

        var totalSeats = Math.Min(includedSeats + sub.PurchasedExtraSeats, maxSeats);

        var occupied = await _db.SeatReservations
            .AsNoTracking()
            .CountAsync(r => r.SubscriptionId == sub.Id
                          && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active), ct);

        return occupied < totalSeats;
    }

    public Task ActivateSeatAsync(int parentUserId, int childId, CancellationToken ct = default,
        string? reservationKey = null)
        => _seatService.ActivateSeatAsync(parentUserId, childId, ct, reservationKey);
}
