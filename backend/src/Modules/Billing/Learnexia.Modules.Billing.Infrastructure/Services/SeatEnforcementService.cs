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
/// Infrastructure implementation of <see cref="ISeatEnforcementService"/> (P10-15-BE-4).
///
/// <para><strong>Enforcement model (lead-approved 2026-06-17):</strong>
/// <list type="bullet">
///   <item>Reconcile: <c>occupiedSeats</c> vs <c>paidActiveSeats</c> for the subscription.</item>
///   <item>Tie-break: earliest-reserved first (<c>SeatReservation.ReservedAt ASC</c>) — oldest seats kept.</item>
///   <item>Lock = <c>SeatState = NoSeatLocked</c> + remove <c>ChildEnergyAllocation</c> row.</item>
///   <item>NO mid-cycle energy forfeit — already-credited energy in the wallet is preserved.</item>
///   <item>Append <c>SeatLedgerEntry{Locked}</c> for each locked child.</item>
///   <item>Also apply <c>RemovalScheduledAt</c> seats that have passed (cycle-end removal).</item>
/// </list>
/// </para>
///
/// <para>All writes run in one explicit transaction. Re-entrancy is safe: state-based guard prevents
/// double-locking already-locked children.</para>
/// </summary>
public sealed class SeatEnforcementService : ISeatEnforcementService
{
    private readonly BillingDbContext _db;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public SeatEnforcementService(
        BillingDbContext db,
        IGlobalSettingsProvider settings,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db          = db;
        _settings    = settings;
        _currentUser = currentUser;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task<int> EnforceAsync(int subscriptionId, CancellationToken ct = default)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var sub = await _db.Subscriptions
                .FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);

            if (sub is null)
            {
                await tx.RollbackAsync(ct);
                _logger.LogInfo($"SeatEnforcementService: subscriptionId={subscriptionId} not found — no-op.");
                return 0;
            }

            var nowUtc = DateTime.UtcNow;

            // ── Finding 2 (P10-15 security audit): apply voluntary-downgrade cancel marker ─
            // At the renewal boundary, PurchasedExtraSeats must be decremented by
            // PendingExtraSeatRemovals (the cycle-end cancel marker set by
            // ScheduleExtraSeatCancellationAsync). Without this step, the paid-seat count
            // incorrectly includes the seats the parent voluntarily cancelled, allowing
            // over-limit children to remain Active indefinitely.
            //
            // Guard: ExtraSeatCancelEffectiveAt <= nowUtc means the renewal boundary has passed.
            // We apply the reduction BEFORE computing paidActiveSeats so the lock logic
            // runs against the correct post-reduction count.
            // The cycleKey used in the idempotency key (Finding 3) is the effective date string.
            string? appliedCycleKey = null;
            if (sub.ExtraSeatCancelEffectiveAt.HasValue
                && sub.ExtraSeatCancelEffectiveAt.Value <= nowUtc
                && sub.PendingExtraSeatRemovals > 0)
            {
                var reduction = Math.Min(sub.PendingExtraSeatRemovals, sub.PurchasedExtraSeats);
                appliedCycleKey = sub.ExtraSeatCancelEffectiveAt.Value.ToString("yyyyMMdd");

                sub.PurchasedExtraSeats       = Math.Max(0, sub.PurchasedExtraSeats - reduction);
                sub.PendingExtraSeatRemovals  = 0;
                sub.ExtraSeatCancelEffectiveAt = null;

                // Write SeatLedgerEntry{RemovedAtRenewal} for the subscription-level reduction.
                // Finding 3: set a deterministic idempotency key so the unique index is the
                // durable backstop against duplicate enforcement runs at the same cycle boundary.
                var removalLedgerKey = $"removed:{sub.Id}:{appliedCycleKey}";
                var alreadyExists = await _db.SeatLedgerEntries
                    .AnyAsync(e => e.IdempotencyKey == removalLedgerKey, ct);

                if (!alreadyExists)
                {
                    var removalLedger = new SeatLedgerEntry
                    {
                        SubscriptionId = subscriptionId,
                        ParentUserId   = sub.ParentUserId,
                        EventType      = SeatLedgerEventType.RemovedAtRenewal,
                        Quantity       = -reduction,
                        OccurredAt     = nowUtc,
                        CreatedAt      = nowUtc,
                        CreatedBy      = _currentUser.UserId ?? 0,
                        IdempotencyKey = removalLedgerKey,
                    };
                    await _db.SeatLedgerEntries.AddAsync(removalLedger, ct);
                }

                _logger.LogInfo(
                    $"SeatEnforcementService: applied cancel marker — subscriptionId={subscriptionId}, " +
                    $"reduction={reduction}, PurchasedExtraSeats now={sub.PurchasedExtraSeats}, " +
                    $"cycleKey={appliedCycleKey}.");
            }

            // ── Resolve paid-active-seat count (AFTER applying cancel marker above) ────────
            var plan = await _db.Plans
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Code == sub.PlanCode, ct);

            var includedSeats = plan?.IncludedSeats
                ?? _settings.GetInt(GlobalSettingKeys.SeatsIncludedFree, 1);
            var maxSeats = _settings.GetInt(GlobalSettingKeys.SeatsMax, 5);
            var paidActiveSeats = Math.Min(includedSeats + sub.PurchasedExtraSeats, maxSeats);

            // ── Load all Active/Reserved reservations for this subscription ──────────────
            // P10-15-BE-10: exclude placeholder rows (ChildId <= 0) — those are transient
            // in-flight add-child reservations that haven't been stamped with a real child id
            // yet. Enforcement must not lock or remove them.
            var reservations = await _db.SeatReservations
                .Where(r => r.SubscriptionId == subscriptionId
                         && r.ChildId > 0
                         && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved))
                .OrderBy(r => r.ReservedAt) // earliest-reserved = kept first
                .ToListAsync(ct);

            // ── Phase 1: Apply scheduled removals (RemovalScheduledAt passed) ────────────
            var scheduledRemovals = reservations
                .Where(r => r.RemovalScheduledAt.HasValue && r.RemovalScheduledAt.Value <= nowUtc)
                .ToList();

            // Phase 1: cycle key for seat-removal idempotency keys (Finding 3).
            var seatRemovalCycleKey = nowUtc.ToString("yyyyMMdd");
            foreach (var removal in scheduledRemovals)
            {
                await RemoveSeatAllocationAsync(removal, sub, SeatLedgerEventType.RemovedAtRenewal,
                    seatRemovalCycleKey, ct);
                reservations.Remove(removal);
            }

            // ── Phase 2: Lock over-limit seats (earliest-reserved tie-break) ─────────────
            var activeSeats = reservations
                .Where(r => r.SeatState == SeatState.Active)
                .ToList();

            if (activeSeats.Count <= paidActiveSeats)
            {
                // No lock enforcement needed — but commit if the cancel marker was applied
                // or scheduled removals ran (both mutate the subscription / ledger).
                var hasPendingWrites = scheduledRemovals.Count > 0 || appliedCycleKey is not null;
                if (hasPendingWrites)
                {
                    await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
                    await tx.CommitAsync(ct);
                }
                else
                {
                    await tx.RollbackAsync(ct);
                }
                _logger.LogInfo(
                    $"SeatEnforcementService: subscriptionId={subscriptionId} no lock needed. " +
                    $"active={activeSeats.Count}, paid={paidActiveSeats}, scheduledRemovals={scheduledRemovals.Count}, " +
                    $"cancelMarkerApplied={appliedCycleKey is not null}.");
                return 0;
            }

            // Over-limit: lock the NEWEST seats (those beyond paidActiveSeats, since list is ordered
            // oldest-first, take the last N over the limit).
            // Finding 3: use a stable per-enforcement-run cycle key for idempotency keys.
            var cycleKey = nowUtc.ToString("yyyyMMdd");
            var seatsToLock = activeSeats.Skip(paidActiveSeats).ToList();
            var lockedCount = 0;

            foreach (var seat in seatsToLock)
            {
                // State guard — skip already-locked (idempotency).
                if (seat.SeatState == SeatState.NoSeatLocked) continue;

                seat.SeatState = SeatState.NoSeatLocked;

                // Remove the child's energy allocation row (no future entitlement).
                var allocation = await _db.ChildEnergyAllocations
                    .FirstOrDefaultAsync(a => a.ChildId == seat.ChildId, ct);
                if (allocation is not null)
                    _db.ChildEnergyAllocations.Remove(allocation);

                // Append SeatLedgerEntry{Locked}.
                // Finding 3: set a deterministic idempotency key — "lock:{subscriptionId}:{childId}:{cycleKey}"
                // — so the unique index on IdempotencyKey is the durable backstop against duplicate
                // enforcement ledger rows in addition to the SeatState guard.
                var ledgerEntry = new SeatLedgerEntry
                {
                    SubscriptionId = subscriptionId,
                    ParentUserId   = sub.ParentUserId,
                    ChildId        = seat.ChildId,
                    EventType      = SeatLedgerEventType.Locked,
                    OccurredAt     = nowUtc,
                    CreatedAt      = nowUtc,
                    CreatedBy      = _currentUser.UserId ?? 0,
                    IdempotencyKey = $"lock:{subscriptionId}:{seat.ChildId}:{cycleKey}",
                };
                await _db.SeatLedgerEntries.AddAsync(ledgerEntry, ct);
                lockedCount++;
            }

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"SeatEnforcementService: subscriptionId={subscriptionId} locked={lockedCount}, " +
                $"scheduledRemovals={scheduledRemovals.Count}, paidSeats={paidActiveSeats}.");
            return lockedCount;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> SweepStalePlaceholdersAsync(DateTime olderThanUtc, CancellationToken ct = default)
    {
        // Orphaned placeholders: still Reserved, never stamped with a real child (ChildId <= 0),
        // reserved before the safety cutoff. ActivateSeatAsync flips them to Active and stamps a
        // positive ChildId; ReleaseSeatAsync flips them to Released — so a row that is STILL a
        // Reserved negative placeholder past the cutoff is double-failure residue.
        var stale = await _db.SeatReservations
            .Where(r => r.ChildId <= 0
                     && r.Status == SeatStatus.Reserved
                     && r.ReservedAt <= olderThanUtc)
            .ToListAsync(ct);

        if (stale.Count == 0)
            return 0;

        var nowUtc = DateTime.UtcNow;
        foreach (var reservation in stale)
        {
            reservation.Status     = SeatStatus.Released;
            reservation.ReleasedAt = nowUtc;
        }

        // Single batched SaveChanges — inherently atomic (one command), no explicit tx needed
        // (ADR-0001: explicit transactions only for atomic MULTI-write paths).
        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

        _logger.LogInfo(
            $"SeatEnforcementService.SweepStalePlaceholders: released {stale.Count} orphaned " +
            $"placeholder reservation(s) reserved on/before {olderThanUtc:O}.");
        return stale.Count;
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────────────────

    /// <param name="cycleKey">
    /// Stable per-enforcement-run date string (yyyyMMdd) used to build a deterministic
    /// idempotency key for the ledger entry (Finding 3).
    /// </param>
    private async Task RemoveSeatAllocationAsync(
        SeatReservation removal,
        Subscription sub,
        SeatLedgerEventType eventType,
        string cycleKey,
        CancellationToken ct)
    {
        removal.Status     = SeatStatus.Released;
        removal.SeatState  = SeatState.NoSeatLocked;
        removal.ReleasedAt = DateTime.UtcNow;

        // Remove the child's energy allocation row.
        var allocation = await _db.ChildEnergyAllocations
            .FirstOrDefaultAsync(a => a.ChildId == removal.ChildId, ct);
        if (allocation is not null)
            _db.ChildEnergyAllocations.Remove(allocation);

        // Append ledger entry.
        // Finding 3: set a deterministic idempotency key for seat-level removal entries.
        // "removed:{subscriptionId}:{childId}:{cycleKey}" differentiates per-seat rows;
        // the subscription-level marker removal uses "removed:{subscriptionId}:{cycleKey}".
        var entry = new SeatLedgerEntry
        {
            SubscriptionId = removal.SubscriptionId,
            ParentUserId   = sub.ParentUserId,
            ChildId        = removal.ChildId,
            EventType      = eventType,
            OccurredAt     = DateTime.UtcNow,
            CreatedAt      = DateTime.UtcNow,
            CreatedBy      = _currentUser.UserId ?? 0,
            IdempotencyKey = $"removed:{removal.SubscriptionId}:{removal.ChildId}:{cycleKey}",
        };
        await _db.SeatLedgerEntries.AddAsync(entry, ct);
    }
}
