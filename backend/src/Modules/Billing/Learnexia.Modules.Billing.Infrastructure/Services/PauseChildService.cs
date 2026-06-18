using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.ChildAccess.Dtos;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IPauseChildService"/> (P10-18-BE-3).
///
/// <para><strong>Algorithm for <see cref="PauseChildAsync"/>:</strong>
/// <list type="number">
///   <item>Load the parent's active <c>SeatReservation</c> for this child (Status IN Active/Reserved,
///         ParentUserId == parentUserId).</item>
///   <item>Family-scope ownership guard: if no matching reservation exists, return
///         <see cref="PauseFailureReason.ChildNotOwnedByParent"/> (IDOR prevention).</item>
///   <item>Idempotency guard: if <c>ParentPauseState</c> is already <c>Paused</c>, return
///         success no-op — no state change, no ledger write.</item>
///   <item>Inside an explicit transaction: set <c>ParentPauseState = Paused</c>, set
///         <c>ParentPauseStateChangedAt = UtcNow</c>, append a
///         <c>SeatLedgerEntry{ParentPause}</c>, commit.</item>
/// </list>
/// </para>
///
/// <para><strong><see cref="UnpauseChildAsync"/>:</strong> mirror logic — same guards, moves
/// <c>ParentPauseState</c> to <c>Active</c>, appends <c>SeatLedgerEntry{ParentUnpause}</c>.</para>
///
/// <para><strong>Zero energy/seat writes in either path.</strong> The ONLY writes are
/// the <c>ParentPauseState</c> flip on the <c>SeatReservation</c> row and the
/// <c>SeatLedgerEntry</c> append.</para>
///
/// <para>No UoW (ADR 0001). Transaction opened explicitly inside each method.</para>
/// </summary>
public sealed class PauseChildService : IPauseChildService
{
    private readonly BillingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public PauseChildService(
        BillingDbContext db,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db          = db;
        _currentUser = currentUser;
        _logger      = logger;
    }

    // ── PauseChildAsync ───────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PauseServiceResult> PauseChildAsync(
        int parentUserId,
        int childId,
        CancellationToken ct = default)
    {
        // ── 1. Load the active seat reservation for this parent + child ───────────
        var seat = await LoadActiveSeatAsync(parentUserId, childId, ct);

        // ── 2. Family-scope ownership guard (IDOR prevention) ─────────────────────
        if (seat is null)
        {
            _logger.LogWarn(
                $"PauseChildService.PauseChildAsync: IDOR rejection — parentUserId={parentUserId}, childId={childId}.");
            return PauseServiceResult.Fail(PauseFailureReason.ChildNotOwnedByParent);
        }

        // ── 3. Idempotency guard ───────────────────────────────────────────────────
        if (seat.ParentPauseState == ParentPauseState.Paused)
        {
            _logger.LogInfo(
                $"PauseChildService.PauseChildAsync: already Paused — no-op. parentUserId={parentUserId}, childId={childId}.");
            return PauseServiceResult.Ok(BuildDto(childId, seat, wasNoOp: true));
        }

        // ── 4. Explicit transaction: flip state + append ledger entry ──────────────
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var nowUtc = DateTime.UtcNow;

            seat.ParentPauseState        = ParentPauseState.Paused;
            seat.ParentPauseStateChangedAt = nowUtc;

            var ledger = BuildLedgerEntry(seat, SeatLedgerEventType.ParentPause, nowUtc, parentUserId, childId);
            _db.SeatLedgerEntries.Add(ledger);

            await _db.SaveChangesAsync(_currentUser.UserId ?? parentUserId);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"PauseChildService.PauseChildAsync: paused. parentUserId={parentUserId}, childId={childId}.");

            return PauseServiceResult.Ok(BuildDto(childId, seat, wasNoOp: false));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── UnpauseChildAsync ─────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<PauseServiceResult> UnpauseChildAsync(
        int parentUserId,
        int childId,
        CancellationToken ct = default)
    {
        // ── 1. Load the active seat reservation ───────────────────────────────────
        var seat = await LoadActiveSeatAsync(parentUserId, childId, ct);

        // ── 2. Family-scope ownership guard ───────────────────────────────────────
        if (seat is null)
        {
            _logger.LogWarn(
                $"PauseChildService.UnpauseChildAsync: IDOR rejection — parentUserId={parentUserId}, childId={childId}.");
            return PauseServiceResult.Fail(PauseFailureReason.ChildNotOwnedByParent);
        }

        // ── 3. Idempotency guard ───────────────────────────────────────────────────
        if (seat.ParentPauseState == ParentPauseState.Active)
        {
            _logger.LogInfo(
                $"PauseChildService.UnpauseChildAsync: already Active — no-op. parentUserId={parentUserId}, childId={childId}.");
            return PauseServiceResult.Ok(BuildDto(childId, seat, wasNoOp: true));
        }

        // ── 4. Explicit transaction: flip state + append ledger entry ──────────────
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var nowUtc = DateTime.UtcNow;

            seat.ParentPauseState        = ParentPauseState.Active;
            seat.ParentPauseStateChangedAt = nowUtc;

            var ledger = BuildLedgerEntry(seat, SeatLedgerEventType.ParentUnpause, nowUtc, parentUserId, childId);
            _db.SeatLedgerEntries.Add(ledger);

            await _db.SaveChangesAsync(_currentUser.UserId ?? parentUserId);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"PauseChildService.UnpauseChildAsync: unpaused. parentUserId={parentUserId}, childId={childId}.");

            return PauseServiceResult.Ok(BuildDto(childId, seat, wasNoOp: false));
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── GetChildAccessStatusAsync ─────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<ChildAccessStatusResult> GetChildAccessStatusAsync(
        int parentUserId,
        int childId,
        CancellationToken ct = default)
    {
        var seat = await LoadActiveSeatAsync(parentUserId, childId, ct);

        if (seat is null)
        {
            _logger.LogWarn(
                $"PauseChildService.GetChildAccessStatusAsync: IDOR rejection — parentUserId={parentUserId}, childId={childId}.");
            return ChildAccessStatusResult.Fail(PauseFailureReason.ChildNotOwnedByParent);
        }

        var isAllowed = seat.SeatState == SeatState.Active
                     && seat.ParentPauseState == ParentPauseState.Active;

        var dto = new ChildAccessStatusDto
        {
            ChildId         = childId,
            SeatState       = seat.SeatState,
            ParentPauseState = seat.ParentPauseState,
            IsAccessAllowed = isAllowed,
        };

        return ChildAccessStatusResult.Ok(dto);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loads the most-recent Active or Reserved <see cref="SeatReservation"/> for the given
    /// parent + child combination. Returns <see langword="null"/> if none exists (IDOR signal).
    /// </summary>
    private Task<SeatReservation?> LoadActiveSeatAsync(int parentUserId, int childId, CancellationToken ct)
        => _db.SeatReservations
            .Where(r => r.ParentUserId == parentUserId
                     && r.ChildId     == childId
                     && (r.Status == SeatStatus.Active || r.Status == SeatStatus.Reserved))
            .OrderByDescending(r => r.ReservedAt)
            .FirstOrDefaultAsync(ct);

    private static PauseResultDto BuildDto(int childId, SeatReservation seat, bool wasNoOp)
        => new()
        {
            ChildId          = childId,
            ParentPauseState = seat.ParentPauseState,
            ChangedAt        = seat.ParentPauseStateChangedAt,
            WasNoOp          = wasNoOp,
        };

    private static SeatLedgerEntry BuildLedgerEntry(
        SeatReservation seat,
        SeatLedgerEventType eventType,
        DateTime occurredAt,
        int parentUserId,
        int childId)
    {
        // IdempotencyKey = "{type}:{parentId}:{childId}:{occurredAt.Ticks}" — PER-OCCURRENCE, NOT
        // per-day. A day-scoped key would collide on the unique SeatLedgerEntries index for a
        // legitimate same-day toggle sequence (pause → unpause → re-pause) and surface as a 500.
        // Genuine duplicate submissions of the SAME action are already collapsed UPSTREAM by the
        // same-state no-op guard (an already-Paused child's re-pause returns a no-op and never
        // reaches this ledger write), so per-occurrence uniqueness is safe.
        var occKey     = occurredAt.Ticks.ToString();
        var typePrefix = eventType == SeatLedgerEventType.ParentPause ? "pause" : "unpause";

        return new SeatLedgerEntry
        {
            SubscriptionId = seat.SubscriptionId,
            // Navigation property intentionally omitted — EF resolves via FK.
            ParentUserId   = parentUserId,
            ChildId        = childId,
            EventType      = eventType,
            OccurredAt     = occurredAt,
            IdempotencyKey = $"{typePrefix}:{parentUserId}:{childId}:{occKey}",
        };
    }
}
