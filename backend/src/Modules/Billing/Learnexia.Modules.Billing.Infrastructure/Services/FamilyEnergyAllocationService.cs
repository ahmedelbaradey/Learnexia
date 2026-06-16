using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IFamilyEnergyAllocationService"/> (P10-13-BE-5).
///
/// <para><strong>Algorithm:</strong>
/// <list type="number">
///   <item>Idempotency pre-check on <c>(parentUserId, cycleStart)</c> via the ledger unique key.</item>
///   <item>Open explicit transaction.</item>
///   <item>Load or create <c>FamilyEnergyAccount</c>.</item>
///   <item>Expire prior-cycle subscription balance + allocation rows (purchased untouched).</item>
///   <item>Deposit <paramref name="grantAmount"/> into <c>FamilyEnergyAccount.SubscriptionBalance</c>.</item>
///   <item>Equal-split across active-seat children: <c>baseShare = grantAmount / N</c>;
///         first <c>(grantAmount % N)</c> children receive <c>baseShare + 1</c>
///         (deterministic remainder; allocated sum == grant exactly).</item>
///   <item>Upsert <c>ChildEnergyAllocation</c> rows; write <c>GrantAllocation</c> ledger entries.</item>
///   <item>Commit.</item>
/// </list>
/// </para>
///
/// <para><strong>Idempotent</strong> on <c>(parentUserId, cycleStart)</c>: duplicate runs return
/// <see cref="AllocationResult.Duplicate"/>.</para>
/// </summary>
public sealed class FamilyEnergyAllocationService : IFamilyEnergyAllocationService
{
    private readonly BillingDbContext _db;
    private readonly ISeatQuery _seatQuery;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public FamilyEnergyAllocationService(
        BillingDbContext db,
        ISeatQuery seatQuery,
        ICurrentUserService currentUser,
        ILoggerManager logger)
    {
        _db          = db;
        _seatQuery   = seatQuery;
        _currentUser = currentUser;
        _logger      = logger;
    }

    /// <inheritdoc/>
    public async Task<AllocationResult> AllocateSubscriptionGrantAsync(
        int parentUserId,
        int grantAmount,
        DateTime cycleStart,
        DateTime cycleEnd,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        // Idempotency pre-check (cheap — avoids opening a transaction for replays).
        var depositKey = $"wallet-grant:{parentUserId}:{idempotencyKey}";
        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == depositKey, ct))
        {
            _logger.LogInfo($"FamilyEnergyAllocationService: idempotent — already allocated for parentUserId={parentUserId}, key={idempotencyKey}.");
            return AllocationResult.Duplicate();
        }

        // Get active-seat children (denominator for equal-split).
        var seatInfo = await _seatQuery.GetActiveSeatsAsync(parentUserId, ct);
        if (seatInfo.ActiveChildIds.Count == 0)
        {
            _logger.LogInfo($"FamilyEnergyAllocationService: no active children for parentUserId={parentUserId} — skipping allocation.");
            return AllocationResult.NoChildren();
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            // Load or create the family wallet.
            var wallet = await _db.FamilyEnergyAccounts
                .FirstOrDefaultAsync(w => w.ParentUserId == parentUserId, ct);

            if (wallet is null)
            {
                wallet = FamilyEnergyAccount.CreateEmpty(parentUserId);
                _db.FamilyEnergyAccounts.Add(wallet);
                await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            }

            // Expire the prior cycle subscription balance (purchased untouched).
            if (wallet.SubscriptionBalance > 0)
            {
                var expireWalletKey = $"wallet-expire:{parentUserId}:{idempotencyKey}";
                var expireTx = wallet.ExpireSubscription(expireWalletKey);
                expireTx.FamilyEnergyAccountId = wallet.Id;
                _db.CreditTransactions.Add(expireTx);
            }

            // Expire prior-cycle allocation rows for these children.
            var priorAllocations = await _db.ChildEnergyAllocations
                .Where(a => a.FamilyEnergyAccountId == wallet.Id
                            && seatInfo.ActiveChildIds.Contains(a.ChildId)
                            && a.CycleEndUtc < cycleStart)
                .ToListAsync(ct);

            foreach (var prior in priorAllocations)
            {
                if (prior.Remaining > 0)
                {
                    var expireAllocKey = $"alloc-expire:{prior.Id}:{idempotencyKey}";
                    var expireAllocTx = prior.Expire(expireAllocKey);
                    expireAllocTx.FamilyEnergyAccountId = wallet.Id;
                    _db.CreditTransactions.Add(expireAllocTx);
                }
            }

            // Deposit the grant into the wallet SubscriptionBalance.
            var depositTx = wallet.DepositSubscriptionGrant(grantAmount, cycleEnd, depositKey);
            _db.CreditTransactions.Add(depositTx);

            // Equal-split with deterministic remainder: first R children get (base+1).
            var n         = seatInfo.ActiveChildIds.Count;
            var baseShare = grantAmount / n;
            var remainder = grantAmount % n;
            var totalAllocated = 0;

            for (var i = 0; i < n; i++)
            {
                var childId = seatInfo.ActiveChildIds[i];
                var share   = baseShare + (i < remainder ? 1 : 0);
                totalAllocated += share;

                // Load or create the ChildEnergyAllocation for this cycle.
                var allocation = await _db.ChildEnergyAllocations
                    .FirstOrDefaultAsync(a => a.FamilyEnergyAccountId == wallet.Id
                                              && a.ChildId            == childId
                                              && a.CycleStartUtc      == cycleStart, ct);

                if (allocation is null)
                {
                    allocation = new ChildEnergyAllocation
                    {
                        FamilyEnergyAccountId = wallet.Id,
                        ChildId               = childId,
                        CycleStartUtc         = cycleStart,
                        CycleEndUtc           = cycleEnd,
                    };
                    _db.ChildEnergyAllocations.Add(allocation);
                    await _db.SaveChangesAsync(_currentUser.UserId ?? 0); // get Id for ledger row
                }

                var allocKey = $"alloc-grant:{wallet.Id}:{childId}:{idempotencyKey}";
                var allocTx  = allocation.Allocate(share, allocKey);
                allocTx.FamilyEnergyAccountId = wallet.Id;
                _db.CreditTransactions.Add(allocTx);
            }

            await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
            await tx.CommitAsync(ct);

            _logger.LogInfo($"FamilyEnergyAllocationService: allocated grant={grantAmount} to {n} children for parentUserId={parentUserId}, cycle={cycleStart:yyyy-MM}.");
            return AllocationResult.Ok(n, totalAllocated);
        }
        catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
        {
            await tx.RollbackAsync(ct);
            _logger.LogInfo($"FamilyEnergyAllocationService: concurrent duplicate for parentUserId={parentUserId} — no-op.");
            return AllocationResult.Duplicate();
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;
}
