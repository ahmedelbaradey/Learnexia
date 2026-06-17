using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ISeatService"/> (Option C, P10-14-BE-3).
///
/// <para>Owns ALL EF Core access, transaction management, seat-state-machine logic,
/// and idempotency. Application-layer handlers inject <see cref="ISeatService"/> and stay EF-free.</para>
///
/// <para><strong>Seat-count formula:</strong>
/// <c>ActivePaidSeats = IncludedSeats(plan tier) + PurchasedExtraSeats</c>
/// capped at <c>seats.max</c> (from <c>IGlobalSettingsProvider</c>).</para>
///
/// <para><strong>Idempotency:</strong> seat reservation is guarded by the unique filtered
/// index on <c>(SubscriptionId, ChildId) WHERE Status IN (Reserved, Active)</c>.
/// A 23505 unique-violation during insert is treated as an idempotent <c>AlreadyReserved</c> result.</para>
///
/// <para><strong>No UoW (ADR 0001):</strong> each write path uses an explicit transaction.</para>
/// </summary>
public sealed class SeatService : ISeatService
{
    private readonly BillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly IPaymentProvider _paymentProvider;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public SeatService(
        BillingDbContext db,
        IGlobalSettingsProvider settings,
        IPaymentProvider paymentProvider,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db              = db;
        _settings        = settings;
        _paymentProvider = paymentProvider;
        _currentUser     = currentUser;
        _logger          = logger;
    }

    // ── GetActiveSeatCountAsync ────────────────────────────────────────────────────────────────

    public async Task<int> GetActiveSeatCountAsync(int parentUserId, CancellationToken ct = default)
    {
        var maxSeats = _settings.GetInt(GlobalSettingKeys.SeatsMax, 5);

        var sub = await GetActiveBillingSubscriptionAsync(parentUserId, ct);

        // BLOCKER-1: null subscription = implicit Free plan (no Subscription row for Free parents).
        // Use IncludedSeats from GlobalSettings + 0 purchased extra seats.
        if (sub is null)
        {
            var freeSeatsFallback = _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);
            return Math.Min(freeSeatsFallback, maxSeats);
        }

        var plan = await _db.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == sub.PlanCode, ct);

        var included = plan?.IncludedSeats
            ?? _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);

        return Math.Min(included + sub.PurchasedExtraSeats, maxSeats);
    }

    // ── ReserveSeatAsync ──────────────────────────────────────────────────────────────────────

    public async Task<SeatReservationServiceResult> ReserveSeatAsync(
        int parentUserId,
        int childId,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var maxSeats = _settings.GetInt(GlobalSettingKeys.SeatsMax, 5);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // ── Find the active subscription ─────────────────────────────────────────────
            // BLOCKER-1: null subscription = implicit Free plan. Free parents have no Subscription
            // row. Provision a Free Active row lazily (GetOrCreate within this transaction) so the
            // SeatReservation FK constraint is satisfied — without auto-creating at registration.
            var sub = await GetOrCreateFreeSubscriptionAsync(parentUserId, ct);

            // ── Resolve included seats from plan ──────────────────────────────────────────
            var plan = await _db.Plans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == sub.PlanCode, ct);

            var includedSeats = plan?.IncludedSeats
                ?? _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);

            var totalSeats = Math.Min(includedSeats + sub.PurchasedExtraSeats, maxSeats);

            // ── Count occupied (Reserved or Active) reservations ─────────────────────────
            var occupied = await _db.SeatReservations
                .Where(r => r.SubscriptionId == sub.Id
                         && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active))
                .CountAsync(ct);

            if (occupied >= totalSeats)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo(
                    $"SeatService.ReserveSeat: no free seat for parent={parentUserId}. occupied={occupied}, total={totalSeats}.");
                return new SeatReservationServiceResult(SeatReservationOutcome.NoFreeSeat, null);
            }

            // ── Insert the reservation ───────────────────────────────────────────────────
            var nowUtc = DateTime.UtcNow;
            var reservation = new SeatReservation
            {
                SubscriptionId = sub.Id,
                ChildId        = childId,
                ParentUserId   = parentUserId,
                Status         = SeatStatus.Reserved,
                ReservedAt     = nowUtc,
                CreatedAt      = nowUtc,
                CreatedBy      = parentUserId,
            };
            await _db.SeatReservations.AddAsync(reservation, ct);
            await _db.SaveChangesAsync(parentUserId);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"SeatService.ReserveSeat: reserved seat for parent={parentUserId}, child={childId}, reservationId={reservation.Id}.");
            return new SeatReservationServiceResult(SeatReservationOutcome.Reserved, reservation.Id);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            // The unique filtered index fired — child already has a Reserved/Active seat.
            _logger.LogInfo(
                $"SeatService.ReserveSeat: idempotent duplicate for parent={parentUserId}, child={childId}.");

            // BLOCKER-2 FIX: use sub.Id (resolved above) in the recovery query, not r.SubscriptionId
            // (self-referential). We look up sub again since the tx was rolled back and sub is tracked.
            // Re-resolve the subscription for the recovery lookup (the tx was rolled back).
            var recoverySub = await GetActiveBillingSubscriptionAsync(parentUserId, ct);
            var recoverySubId = recoverySub?.Id;

            var existing = recoverySubId.HasValue
                ? await _db.SeatReservations
                    .AsNoTracking()
                    .Where(r => r.SubscriptionId == recoverySubId.Value
                             && r.ChildId == childId
                             && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active))
                    .Select(r => (int?)r.Id)
                    .FirstOrDefaultAsync(ct)
                : null;

            return new SeatReservationServiceResult(SeatReservationOutcome.AlreadyReserved, existing);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── ReleaseSeatAsync ──────────────────────────────────────────────────────────────────────
    //
    // NOTE (P10-14-BE-5): During compensation after a failed AddChild, the handler calls
    // ReleaseSeatAsync with childId=0 (the placeholder used at reservation time).
    // This method releases the most-recent unlinked placeholder for this parent+subscription
    // when childId=0, and uses direct childId lookup otherwise.

    public async Task ReleaseSeatAsync(int parentUserId, int childId, CancellationToken ct = default)
    {
        var sub = await GetActiveBillingSubscriptionAsync(parentUserId, ct);
        if (sub is null) return;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            SeatReservation? reservation;

            if (childId == 0)
            {
                // Compensation path: find the most-recent unlinked placeholder.
                reservation = await _db.SeatReservations
                    .Where(r => r.SubscriptionId == sub.Id
                             && r.ChildId == 0
                             && r.ParentUserId == parentUserId
                             && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active))
                    .OrderByDescending(r => r.ReservedAt)
                    .FirstOrDefaultAsync(ct);
            }
            else
            {
                reservation = await _db.SeatReservations
                    .Where(r => r.SubscriptionId == sub.Id
                             && r.ChildId == childId
                             && (r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active))
                    .FirstOrDefaultAsync(ct);
            }

            if (reservation is null)
            {
                await tx.RollbackAsync(ct);
                return; // Idempotent — already released or never existed.
            }

            reservation.Status     = SeatStatus.Released;
            reservation.ReleasedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync(parentUserId);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"SeatService.ReleaseSeat: released seat for parent={parentUserId}, child={childId}.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── ActivateSeatAsync ─────────────────────────────────────────────────────────────────────
    //
    // NOTE (P10-14-BE-5): During AddChild, the seat is Reserved with childId=0 (placeholder,
    // child not yet created). On success the handler calls ActivateSeatAsync with the real
    // childId. This method therefore finds the most-recent placeholder (childId=0, Reserved)
    // row for this subscription, stamps the real childId, and flips status to Active.
    // For already-linked reservations (childId != 0), a direct lookup by childId is used.

    public async Task ActivateSeatAsync(int parentUserId, int childId, CancellationToken ct = default)
    {
        var sub = await GetActiveBillingSubscriptionAsync(parentUserId, ct);
        if (sub is null) return;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            SeatReservation? reservation;

            // Try direct lookup first (idempotent: already linked or already active).
            reservation = await _db.SeatReservations
                .Where(r => r.SubscriptionId == sub.Id
                         && r.ChildId == childId
                         && r.Status == SeatStatus.Reserved)
                .FirstOrDefaultAsync(ct);

            // If not found (placeholder path: childId=0 was used during reserve), find the
            // most-recent unlinked placeholder row for this parent and link it now.
            if (reservation is null && childId != 0)
            {
                reservation = await _db.SeatReservations
                    .Where(r => r.SubscriptionId == sub.Id
                             && r.ChildId == 0
                             && r.ParentUserId == parentUserId
                             && r.Status == SeatStatus.Reserved)
                    .OrderByDescending(r => r.ReservedAt)
                    .FirstOrDefaultAsync(ct);

                if (reservation is not null)
                    reservation.ChildId = childId; // stamp the real child id
            }

            if (reservation is null)
            {
                // Already Active or not found — idempotent.
                await tx.RollbackAsync(ct);
                return;
            }

            reservation.Status = SeatStatus.Active;

            await _db.SaveChangesAsync(parentUserId);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"SeatService.ActivateSeat: activated seat for parent={parentUserId}, child={childId}.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── GetSeatStatusAsync ────────────────────────────────────────────────────────────────────

    public async Task<SeatStatusSnapshot> GetSeatStatusAsync(int parentUserId, CancellationToken ct = default)
    {
        var maxSeats = _settings.GetInt(GlobalSettingKeys.SeatsMax, 5);

        // BLOCKER-1: null subscription = implicit Free plan.
        var sub = await GetActiveBillingSubscriptionAsync(parentUserId, ct);

        if (sub is null)
        {
            // Free plan with no Subscription row: return the Free entitlement (no reservations yet).
            var freeIncluded = _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);
            var freeTotalSeats = Math.Min(freeIncluded, maxSeats);
            return new SeatStatusSnapshot(
                IncludedSeats      : freeIncluded,
                PurchasedExtraSeats: 0,
                TotalSeats         : freeTotalSeats,
                OccupiedSeats      : 0,
                AvailableSeats     : freeTotalSeats,
                MaxSeats           : maxSeats,
                Children           : []);
        }

        var plan = await _db.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == sub.PlanCode, ct);

        var includedSeats = plan?.IncludedSeats
            ?? _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);

        var totalSeats = Math.Min(includedSeats + sub.PurchasedExtraSeats, maxSeats);

        var reservations = await _db.SeatReservations
            .AsNoTracking()
            .Where(r => r.SubscriptionId == sub.Id)
            .OrderBy(r => r.ReservedAt)
            .Select(r => new { r.ChildId, r.Status })
            .ToListAsync(ct);

        var occupied = reservations
            .Count(r => r.Status == SeatStatus.Reserved || r.Status == SeatStatus.Active);

        var children = reservations
            .Select(r => new ChildSeatStatusDto(r.ChildId, r.Status.ToString()))
            .ToList();

        return new SeatStatusSnapshot(
            IncludedSeats      : includedSeats,
            PurchasedExtraSeats: sub.PurchasedExtraSeats,
            TotalSeats         : totalSeats,
            OccupiedSeats      : occupied,
            AvailableSeats     : Math.Max(0, totalSeats - occupied),
            MaxSeats           : maxSeats,
            Children           : children);
    }

    // NOTE (P10-14, security #2): the purchased-seat increment is NOT a standalone service method.
    // It lives inline in WebhookEventService's Seat branch so it commits inside the webhook's single
    // transaction (BLOCKER-3 atomicity) alongside the Payment flip + WebhookEvent record. That branch
    // owns the seats.max ceiling guard AND the per-payment idempotency (Status==Initiated) guard. A
    // separate method here would open its own transaction (cannot join the webhook's) and become a
    // divergent second copy of the money-mutation guard — so it was removed.

    // ── ScheduleExtraSeatCancellationAsync ──────────────────────────────────────────────────────
    //
    // P10-14-BE-8 / OQ-5 (lead-approved 2026-06-17): a VOLUNTARY extra-seat cancel is effective at
    // CYCLE END — NOT instant, NOT grace-triggered. We record a scheduled-removal marker only:
    //   • PurchasedExtraSeats is NOT decremented mid-cycle (the seat stays Active; RealSeatQuery /
    //     the P10-13 grant denominator are unchanged for the rest of the cycle).
    //   • GraceEndsAt is NEVER touched — grace is the payment-failure retry window only; conflating
    //     a voluntary cancel with it would fabricate a fake dunning state P10-15 would act on.
    //   • No energy is reclaimed/forfeited/refunded; no child is deleted.
    // At renewal, P10-15 applies the removal (PurchasedExtraSeats -= PendingExtraSeatRemovals; reset
    // the marker) and moves over-seat children to NoSeat/Locked.

    public async Task<SeatCancelResult> ScheduleExtraSeatCancellationAsync(
        int parentUserId,
        int seatQuantity,
        CancellationToken ct = default)
    {
        if (seatQuantity <= 0)
            return new SeatCancelResult(false, "QuantityMustBePositive");

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var sub = await GetActiveBillingSubscriptionAsync(parentUserId, ct);
            if (sub is null)
            {
                await tx.RollbackAsync(ct);
                return new SeatCancelResult(false, "SubscriptionNotFound");
            }

            // Cannot schedule the removal of more extra seats than are purchased net of what is
            // already pending removal (idempotent accumulation across repeated cancels).
            if (seatQuantity > sub.PurchasedExtraSeats - sub.PendingExtraSeatRemovals)
            {
                await tx.RollbackAsync(ct);
                return new SeatCancelResult(false, "InsufficientExtraSeats");
            }

            // Record the scheduled-removal marker — effective at the next renewal boundary.
            // NOTE: PurchasedExtraSeats and GraceEndsAt are intentionally left UNTOUCHED.
            sub.PendingExtraSeatRemovals += seatQuantity;
            sub.ExtraSeatCancelEffectiveAt = sub.CurrentCycleEnd;

            await _db.SaveChangesAsync(parentUserId);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"SeatService.ScheduleExtraSeatCancellation: parent={parentUserId}, scheduled -{seatQuantity} extra seat(s) " +
                $"for removal at cycle end (pending={sub.PendingExtraSeatRemovals}, effectiveAt={sub.ExtraSeatCancelEffectiveAt:O}). " +
                $"PurchasedExtraSeats unchanged ({sub.PurchasedExtraSeats}); GraceEndsAt untouched.");

            return new SeatCancelResult(true, null);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── StartSeatCheckoutAsync ────────────────────────────────────────────────────────────────

    public async Task<SeatCheckoutResult> StartSeatCheckoutAsync(
        int parentUserId,
        int seatQuantity,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        var maxSeats = _settings.GetInt(GlobalSettingKeys.SeatsMax, 5);

        // ── 1. Load active subscription ───────────────────────────────────────────────
        var sub = await GetActiveBillingSubscriptionAsync(parentUserId, ct);
        if (sub is null)
        {
            _logger.LogInfo(
                $"SeatService.StartSeatCheckout: no active subscription for parent={parentUserId}.");
            return SeatCheckoutResult.Fail(SeatCheckoutFailureReason.NoActiveSubscription);
        }

        // ── 2. Free-plan gate (OQ-C) ──────────────────────────────────────────────────
        if (sub.PlanCode == PlanCode.Free)
        {
            _logger.LogInfo(
                $"SeatService.StartSeatCheckout: Free plan parent={parentUserId} attempted extra-seat checkout — rejected.");
            return SeatCheckoutResult.Fail(SeatCheckoutFailureReason.FreePlanNotAllowed);
        }

        // ── 3. Max-seats ceiling guard ────────────────────────────────────────────────
        var plan = await _db.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Code == sub.PlanCode, ct);
        var includedSeats = plan?.IncludedSeats
            ?? _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);

        var afterPurchase = includedSeats + sub.PurchasedExtraSeats + seatQuantity;
        if (afterPurchase > maxSeats)
        {
            _logger.LogInfo(
                $"SeatService.StartSeatCheckout: parent={parentUserId} — buying {seatQuantity} would exceed max={maxSeats}.");
            return SeatCheckoutResult.Fail(SeatCheckoutFailureReason.ExceedsMaxSeats);
        }

        // ── 4. Resolve price SERVER-SIDE + PRORATE the mid-cycle charge ──────────────
        // P10-14 (locked 2026-06-17): adding a seat mid-cycle charges MONEY prorated by the
        // fraction of the current billing cycle still remaining (SeatPrice × qty × remaining-ratio).
        // The ratio is computed SERVER-SIDE from the subscription cycle dates — never client-supplied.
        // NO energy is minted here (the webhook seat branch only bumps PurchasedExtraSeats).
        var pricePerSeat   = _settings.GetDecimal(GlobalSettingKeys.SeatsExtraPriceEgp, 169m);
        var fullAmount     = pricePerSeat * seatQuantity;
        var remainingRatio = ComputeRemainingCycleRatio(sub, DateTime.UtcNow);
        var totalAmount    = Math.Round(fullAmount * remainingRatio, 2, MidpointRounding.AwayFromZero);

        // ── 5. Create Payment row (Initiated) ─────────────────────────────────────────
        var payment = new Payment
        {
            ParentUserId   = parentUserId,
            SubscriptionId = sub.Id,
            Amount         = totalAmount,
            Currency       = PaymentCurrency.EGP,
            Status         = PaymentStatus.Initiated,
            Kind           = PaymentKind.Seat,
            IdempotencyKey = idempotencyKey,
            CreatedAt      = DateTime.UtcNow,
            CreatedBy      = _currentUser.UserId ?? parentUserId,
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(_currentUser.UserId ?? parentUserId);

        // ── 6. Call the payment provider ──────────────────────────────────────────────
        CheckoutSession session;
        try
        {
            session = await _paymentProvider.CreateCheckoutSessionAsync(payment, ct);
        }
        catch (Exception provEx)
        {
            _logger.LogError(provEx,
                $"SeatService.StartSeatCheckout: provider error for parent={parentUserId}, paymentId={payment.Id}.");
            return SeatCheckoutResult.Fail(SeatCheckoutFailureReason.ProviderUnavailable);
        }

        // ── 7. Persist provider ref ───────────────────────────────────────────────────
        payment.ProviderPaymentRef = session.ProviderPaymentRef;
        await _db.SaveChangesAsync(_currentUser.UserId ?? parentUserId);

        _logger.LogInfo(
            $"SeatService.StartSeatCheckout: created seat checkout for parent={parentUserId}, " +
            $"qty={seatQuantity}, fullAmount={fullAmount:F2}, ratio={remainingRatio:F4}, " +
            $"proratedAmount={totalAmount:F2}, paymentId={payment.Id}.");

        return SeatCheckoutResult.Ok(payment.Id, session.RedirectUrl, session.ProviderPaymentRef, totalAmount);
    }

    /// <summary>
    /// Computes the fraction of the current billing cycle still remaining at
    /// <paramref name="nowUtc"/> — the mid-cycle money-proration multiplier
    /// (<c>(end − now) / (end − start)</c>), clamped to <c>[0, 1]</c>.
    ///
    /// <para>Falls back to <c>1.0</c> (full price — the conservative, never-undercharge default)
    /// when the cycle dates are absent or inconsistent (e.g. a Free-origin row with no cycle, or
    /// <c>now</c> already past the cycle end where the renewal will charge a full cycle anyway).</para>
    /// </summary>
    private static decimal ComputeRemainingCycleRatio(Subscription sub, DateTime nowUtc)
    {
        if (sub.CurrentCycleStart is not { } start || sub.CurrentCycleEnd is not { } end)
            return 1m;                       // no cycle window → charge full (never undercharge)

        // Normalize to UTC before comparing. The Host enables Npgsql legacy timestamp behavior
        // (Program.cs: EnableLegacyTimestampBehavior=true), so `timestamptz` columns read back as
        // DateTimeKind.Local — subtracting them from DateTime.UtcNow directly would inject the
        // local (Cairo) offset and skew the prorated charge. ToUniversalTime() recovers the true instant.
        start = start.Kind == DateTimeKind.Utc ? start : start.ToUniversalTime();
        end   = end.Kind   == DateTimeKind.Utc ? end   : end.ToUniversalTime();

        if (end <= start) return 1m;         // inconsistent dates → charge full
        if (nowUtc <= start) return 1m;      // at/before cycle start → full cycle ahead
        if (nowUtc >= end) return 1m;        // cycle already elapsed → renewal charges full; don't give a near-free seat

        var totalTicks     = (decimal)(end - start).Ticks;
        var remainingTicks = (decimal)(end - nowUtc).Ticks;
        return remainingTicks / totalTicks;  // ∈ (0, 1) for a genuine mid-cycle add
    }

    // ── Private helpers ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the parent's currently billing-active subscription (Active, PastDue, or Dunning —
    /// all states where seats remain active including during grace). Returns null if none exists.
    /// </summary>
    private Task<Subscription?> GetActiveBillingSubscriptionAsync(int parentUserId, CancellationToken ct)
        => _db.Subscriptions
            .Where(s => s.ParentUserId == parentUserId
                     && (s.Status == SubscriptionStatus.Active
                         || s.Status == SubscriptionStatus.PastDue
                         || s.Status == SubscriptionStatus.Dunning))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// BLOCKER-1 fix: GetOrCreate a billing-active Subscription for the parent.
    ///
    /// <para>Free parents have no Subscription row at registration time (the row is never
    /// pre-created). When the first seat is reserved we lazily provision a Free Active row so
    /// <see cref="SeatReservation.SubscriptionId"/> (a required FK) can be satisfied — without
    /// creating the row at parent sign-up. Idempotent: if the row already exists it is returned.</para>
    ///
    /// <para>Must be called INSIDE an open transaction so the upsert is atomic with the
    /// subsequent <see cref="SeatReservation"/> insert.</para>
    /// </summary>
    private async Task<Subscription> GetOrCreateFreeSubscriptionAsync(int parentUserId, CancellationToken ct)
    {
        var existing = await GetActiveBillingSubscriptionAsync(parentUserId, ct);
        if (existing is not null)
            return existing;

        // No billing-active subscription: provision a Free+Active row lazily.
        var nowUtc = DateTime.UtcNow;
        var freeSub = new Subscription
        {
            ParentUserId  = parentUserId,
            PlanCode      = PlanCode.Free,
            Status        = SubscriptionStatus.Active,
            BillingPeriod = BillingPeriod.Monthly,
            CreatedAt     = nowUtc,
            CreatedBy     = parentUserId,
        };
        await _db.Subscriptions.AddAsync(freeSub, ct);
        await _db.SaveChangesAsync(parentUserId);

        _logger.LogInfo(
            $"SeatService.GetOrCreateFreeSubscription: provisioned Free subscription id={freeSub.Id} for parent={parentUserId}.");

        return freeSub;
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_SeatReservations", StringComparison.OrdinalIgnoreCase) == true;
}
