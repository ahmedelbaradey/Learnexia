using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IFamilyAllocationService"/> (P10-16-BE-3).
///
/// <para><strong>Algorithm for <see cref="TransferAllocationAsync"/>:</strong>
/// <list type="number">
///   <item>Idempotency pre-check on <paramref name="idempotencyKey"/> — short-circuits duplicate POSTs.</item>
///   <item>Load the parent's <c>FamilyEnergyAccount</c>; reject if absent.</item>
///   <item>Verify <paramref name="fromChildId"/> and <paramref name="toChildId"/> both hold active seats
///         belonging to this family (<strong>same-family guard — anti cross-family / IDOR</strong>).</item>
///   <item>Reject <c>amount ≤ 0</c> or <c>amount &gt; source.Remaining</c>
///         (<strong>only UNSPENT allocated energy is movable; already-spent never reclaimable</strong>).</item>
///   <item>Reject <c>source == destination</c>.</item>
///   <item>Load destination allocation row; if absent for the current cycle
///         (<strong>mid-cycle-added child</strong>), INSERT a new row with
///         <c>Allocated=0, Spent=0</c> before applying the transfer.</item>
///   <item>Open one explicit transaction: decrement source, increment destination, append paired
///         <c>TransferOut</c>/<c>TransferIn</c> ledger rows (same <c>CorrelationId</c>), commit.</item>
///   <item>The transfer is <strong>bucket A only</strong> — the shared purchased reserve is never read or written.</item>
/// </list>
/// </para>
///
/// <para><strong>No UoW (ADR 0001):</strong> explicit transaction in this service.</para>
/// <para><strong>Option C:</strong> Application handlers inject <see cref="IFamilyAllocationService"/> only.</para>
/// </summary>
public sealed class FamilyAllocationService : IFamilyAllocationService
{
    private readonly BillingDbContext _db;
    private readonly ISeatStateQuery _seatStateQuery;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public FamilyAllocationService(
        BillingDbContext db,
        ISeatStateQuery seatStateQuery,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db             = db;
        _seatStateQuery = seatStateQuery;
        _currentUser    = currentUser;
        _logger         = logger;
    }

    // ── GetFamilyAllocationAsync ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<FamilyAllocationDto?> GetFamilyAllocationAsync(
        int parentUserId,
        CancellationToken ct = default)
    {
        // Load the family wallet.
        var wallet = await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentUserId, ct);

        if (wallet is null)
            return null;

        // Load all active-seat children for this parent (via SeatReservation, Billing-owned).
        var activeChildIds = await GetActiveSeatChildIdsAsync(wallet.Id, ct);

        if (activeChildIds.Count == 0)
            return new FamilyAllocationDto { Children = Array.Empty<ChildAllocationItemDto>() };

        // Load the most-recent allocation row per child (max CycleStartUtc).
        var allAllocations = await _db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == wallet.Id
                     && activeChildIds.Contains(a.ChildId))
            .OrderByDescending(a => a.CycleStartUtc)
            .ToListAsync(ct);

        // Index: childId → latest allocation row.
        var latestPerChild = allAllocations
            .GroupBy(a => a.ChildId)
            .ToDictionary(g => g.Key, g => g.First());

        var children = activeChildIds
            .Select(childId =>
            {
                if (latestPerChild.TryGetValue(childId, out var alloc))
                {
                    return new ChildAllocationItemDto
                    {
                        ChildId          = childId,
                        Allocated        = alloc.AllocatedAmount,
                        Spent            = alloc.SpentAmount,
                        Remaining        = alloc.Remaining,
                        IsMidCycleNoGrant = false,
                    };
                }

                // No allocation row for this child — mid-cycle-added child with no grant.
                return new ChildAllocationItemDto
                {
                    ChildId          = childId,
                    Allocated        = 0,
                    Spent            = 0,
                    Remaining        = 0,
                    IsMidCycleNoGrant = true,
                };
            })
            .ToList();

        return new FamilyAllocationDto { Children = children };
    }

    // ── TransferAllocationAsync ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<TransferAllocationResult> TransferAllocationAsync(
        int parentId,
        int fromChildId,
        int toChildId,
        long amount,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        // ── Step 1: Idempotency pre-check ──────────────────────────────────────────────────
        var outKey = TransferOutKey(idempotencyKey);
        var existing = await _db.CreditTransactions
            .AsNoTracking()
            .Where(t => t.IdempotencyKey == outKey)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: idempotent replay for key={idempotencyKey}, " +
                $"parent={parentId}.");

            // Rebuild the result from current state (idempotent: return prior outcome).
            return await BuildIdempotentResultAsync(parentId, fromChildId, toChildId, ct);
        }

        // ── Step 2: Load wallet ────────────────────────────────────────────────────────────
        var wallet = await _db.FamilyEnergyAccounts
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId, ct);

        if (wallet is null)
        {
            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: wallet not found for parent={parentId}.");
            return TransferAllocationResult.Fail(TransferAllocationFailureReason.WalletNotFound);
        }

        // ── Step 3: Same-family guard (anti-IDOR, anti-cross-family) ──────────────────────
        // Validate both children hold Active SeatReservations tied to this FamilyEnergyAccount.
        var activeSeatIds = await GetActiveSeatChildIdsAsync(wallet.Id, ct);

        if (!activeSeatIds.Contains(fromChildId))
        {
            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: fromChildId={fromChildId} not in family for parent={parentId}.");
            return TransferAllocationResult.Fail(TransferAllocationFailureReason.ChildNotInFamily);
        }

        if (!activeSeatIds.Contains(toChildId))
        {
            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: toChildId={toChildId} not in family for parent={parentId}.");
            return TransferAllocationResult.Fail(TransferAllocationFailureReason.ChildNotInFamily);
        }

        // ── Step 4: Source == destination guard ────────────────────────────────────────────
        if (fromChildId == toChildId)
        {
            return TransferAllocationResult.Fail(TransferAllocationFailureReason.SameSourceAndDestination);
        }

        // ── Step 5: Active-seat verification via ISeatStateQuery ──────────────────────────
        // A child in activeSeatIds came from Active SeatReservations; also use ISeatStateQuery
        // for the SeatState==Active check (not just Status=Active, also SeatState==Active).
        var fromSeatActive = await _seatStateQuery.IsChildSeatActiveAsync(fromChildId, ct);
        if (!fromSeatActive)
        {
            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: fromChildId={fromChildId} has no active seat (SeatState).");
            return TransferAllocationResult.Fail(TransferAllocationFailureReason.SourceHasNoActiveSeat);
        }

        var toSeatActive = await _seatStateQuery.IsChildSeatActiveAsync(toChildId, ct);
        if (!toSeatActive)
        {
            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: toChildId={toChildId} has no active seat (SeatState).");
            return TransferAllocationResult.Fail(TransferAllocationFailureReason.DestinationHasNoActiveSeat);
        }

        // ── Step 6: Load source allocation row ────────────────────────────────────────────
        var sourceAlloc = await GetCurrentCycleAllocationAsync(wallet.Id, fromChildId, ct);

        if (sourceAlloc is null || sourceAlloc.Remaining < amount || amount <= 0)
        {
            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: amount={amount} exceeds source remaining=" +
                $"{sourceAlloc?.Remaining ?? 0} for fromChildId={fromChildId}.");
            return TransferAllocationResult.Fail(TransferAllocationFailureReason.AmountExceedsRemaining);
        }

        // ── Step 7: Atomic transaction ─────────────────────────────────────────────────────
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Re-load source with tracking for mutation.
            var sourceTrk = await GetCurrentCycleAllocationAsync(wallet.Id, fromChildId, ct, tracked: true);
            if (sourceTrk is null || sourceTrk.Remaining < amount)
            {
                // Concurrency: another request consumed the remaining between our read and lock.
                await tx.RollbackAsync(ct);
                return TransferAllocationResult.Fail(TransferAllocationFailureReason.AmountExceedsRemaining);
            }

            // Load or INSERT destination allocation row.
            var destAlloc = await GetCurrentCycleAllocationAsync(wallet.Id, toChildId, ct, tracked: true);
            if (destAlloc is null)
            {
                // Mid-cycle-added child with no allocation row: INSERT one with Allocated=0, Spent=0.
                destAlloc = new ChildEnergyAllocation
                {
                    FamilyEnergyAccountId = wallet.Id,
                    ChildId               = toChildId,
                    // Use the source child's cycle dates (same cycle, same wallet).
                    CycleStartUtc         = sourceTrk.CycleStartUtc,
                    CycleEndUtc           = sourceTrk.CycleEndUtc,
                    AllocatedAmount       = 0,
                    SpentAmount           = 0,
                };
                _db.ChildEnergyAllocations.Add(destAlloc);
                await _db.SaveChangesAsync(_currentUser.UserId ?? 0);

                _logger.LogInfo(
                    $"FamilyAllocationService.Transfer: INSERTed mid-cycle allocation row for " +
                    $"toChildId={toChildId}, walletId={wallet.Id}.");
            }

            // Generate a shared CorrelationId for the paired ledger entries.
            var correlationId = Guid.NewGuid();

            // Decrement source (uses ChildEnergyAllocation.SendTransfer mutator).
            var sendTx = sourceTrk.SendTransfer((int)amount, outKey, correlationId);
            sendTx.FamilyEnergyAccountId = wallet.Id;
            _db.CreditTransactions.Add(sendTx);

            // Increment destination (uses ChildEnergyAllocation.ReceiveTransfer mutator).
            var receiveKey = TransferInKey(idempotencyKey);
            var receiveTx = destAlloc.ReceiveTransfer((int)amount, receiveKey, correlationId);
            receiveTx.FamilyEnergyAccountId = wallet.Id;
            _db.CreditTransactions.Add(receiveTx);

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: transferred amount={amount} from " +
                $"childId={fromChildId} to childId={toChildId} for parent={parentId}, " +
                $"correlationId={correlationId}.");

            var resultData = new TransferResultDto
            {
                FromChild = new ChildAllocationItemDto
                {
                    ChildId   = fromChildId,
                    Allocated = sourceTrk.AllocatedAmount,
                    Spent     = sourceTrk.SpentAmount,
                    Remaining = sourceTrk.Remaining,
                    IsMidCycleNoGrant = false,
                },
                ToChild = new ChildAllocationItemDto
                {
                    ChildId   = toChildId,
                    Allocated = destAlloc.AllocatedAmount,
                    Spent     = destAlloc.SpentAmount,
                    Remaining = destAlloc.Remaining,
                    IsMidCycleNoGrant = false,
                },
            };

            return TransferAllocationResult.Ok(resultData);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            _logger.LogInfo(
                $"FamilyAllocationService.Transfer: unique-key duplicate for key={idempotencyKey} — returning idempotent result.");
            return await BuildIdempotentResultAsync(parentId, fromChildId, toChildId, ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    // ── Private helpers ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all active-seat child ids that belong to the given <paramref name="familyEnergyAccountId"/>.
    ///
    /// <para>Uses <c>SeatReservation</c> rows owned by Billing — no cross-module FK.
    /// The parent's subscription is resolved via <c>FamilyEnergyAccount.ParentUserId → Subscription.ParentUserId</c>.
    /// Only children with <c>SeatStatus.Active</c> AND <c>SeatState.Active</c> are returned.</para>
    /// </summary>
    private async Task<IReadOnlyList<int>> GetActiveSeatChildIdsAsync(
        int familyEnergyAccountId,
        CancellationToken ct)
    {
        // Resolve parentUserId from the wallet.
        var parentUserId = await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .Where(w => w.Id == familyEnergyAccountId)
            .Select(w => w.ParentUserId)
            .FirstOrDefaultAsync(ct);

        if (parentUserId == 0)
            return Array.Empty<int>();

        // Resolve active subscription (Active, PastDue, or Dunning — seats remain active during grace).
        var subId = await _db.Subscriptions
            .AsNoTracking()
            .Where(s => s.ParentUserId == parentUserId
                     && (s.Status == Domain.Enums.SubscriptionStatus.Active
                         || s.Status == Domain.Enums.SubscriptionStatus.PastDue
                         || s.Status == Domain.Enums.SubscriptionStatus.Dunning))
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);

        if (subId is null)
            return Array.Empty<int>();

        return await _db.SeatReservations
            .AsNoTracking()
            .Where(r => r.SubscriptionId == subId.Value
                     && r.ChildId > 0
                     && (r.Status == Domain.Enums.SeatStatus.Active || r.Status == Domain.Enums.SeatStatus.Reserved)
                     && r.SeatState == Domain.Enums.SeatState.Active)
            .Select(r => r.ChildId)
            .Distinct()
            .ToListAsync(ct);
    }

    /// <summary>
    /// Loads the most recent (current-cycle) <c>ChildEnergyAllocation</c> for a child
    /// within the given wallet. Returns null if no row exists for the current cycle.
    /// </summary>
    private Task<ChildEnergyAllocation?> GetCurrentCycleAllocationAsync(
        int familyEnergyAccountId,
        int childId,
        CancellationToken ct,
        bool tracked = false)
    {
        var query = tracked
            ? _db.ChildEnergyAllocations
            : _db.ChildEnergyAllocations.AsNoTracking();

        return query
            .Where(a => a.FamilyEnergyAccountId == familyEnergyAccountId
                     && a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Builds a <see cref="TransferAllocationResult"/> from the current allocation state
    /// (used on idempotency replay — the prior transfer already committed).
    /// </summary>
    private async Task<TransferAllocationResult> BuildIdempotentResultAsync(
        int parentId,
        int fromChildId,
        int toChildId,
        CancellationToken ct)
    {
        var wallet = await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId, ct);

        if (wallet is null)
            return TransferAllocationResult.Fail(TransferAllocationFailureReason.WalletNotFound);

        var fromAlloc = await GetCurrentCycleAllocationAsync(wallet.Id, fromChildId, ct);
        var toAlloc   = await GetCurrentCycleAllocationAsync(wallet.Id, toChildId, ct);

        return TransferAllocationResult.Duplicate(new TransferResultDto
        {
            FromChild = fromAlloc is not null
                ? new ChildAllocationItemDto
                  {
                      ChildId   = fromChildId,
                      Allocated = fromAlloc.AllocatedAmount,
                      Spent     = fromAlloc.SpentAmount,
                      Remaining = fromAlloc.Remaining,
                  }
                : new ChildAllocationItemDto { ChildId = fromChildId },
            ToChild = toAlloc is not null
                ? new ChildAllocationItemDto
                  {
                      ChildId   = toChildId,
                      Allocated = toAlloc.AllocatedAmount,
                      Spent     = toAlloc.SpentAmount,
                      Remaining = toAlloc.Remaining,
                  }
                : new ChildAllocationItemDto { ChildId = toChildId },
        });
    }

    // Deterministic idempotency key derivation — paired out/in keys from a single client key.
    private static string TransferOutKey(string key) => $"transfer-out:{key}";
    private static string TransferInKey(string key)  => $"transfer-in:{key}";

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;
}
