using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Modules.Billing.Application.Features.EnergyStatus.Dtos;
using Learnexia.Modules.Billing.Application.Services;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Options;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ICreditLedgerService"/> (Option C).
///
/// <para><strong>CreditAccount RETIRED:</strong> All per-child <c>CreditAccount</c> paths
/// (GrantAsync on per-child, RefundAsync, AdjustAsync, ExpireGrantAsync, ApplyPurchaseAsync,
/// GetCreditAccountAsync, per-child ReconcileAsync) have been removed. Energy movement is
/// entirely through the Family Wallet + the immutable <c>CreditTransaction</c> ledger.</para>
///
/// <para><strong>Retained active methods:</strong>
/// <list type="bullet">
///   <item><see cref="GrantFamilyWalletAsync"/> — admin comp deposit to the family <c>PurchasedBalance</c>.</item>
///   <item><see cref="SpendAsync"/> — wallet debit (allocation-first → purchased fallback).</item>
///   <item><see cref="ReconcileFamilyWalletAsync"/> — recompute family wallet from the ledger.</item>
///   <item><see cref="GetEnergyStatusAsync"/> — per-child energy status from the wallet.</item>
/// </list>
/// </para>
///
/// <para><strong>Optimistic-concurrency retry (SpendAsync):</strong> debits load the allocation
/// row via <c>xmin</c> concurrency token. On <c>DbUpdateConcurrencyException</c> the handler
/// retries up to <c>MaxRetries</c> times (ChangeTracker cleared between attempts), with
/// exponential back-off and jitter between each retry.</para>
///
/// <para><strong>23505 idempotency guard:</strong> unique-key violations on
/// <c>UX_CreditTransactions_IdempotencyKey</c> are caught and treated as idempotent
/// success — no second write.</para>
/// </summary>
public sealed class CreditLedgerService : ICreditLedgerService
{
    private readonly BillingDbContext _db;
    private readonly ILoggerManager _logger;
    private readonly BillingConcurrencyOptions _concurrency;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemClock _clock;

    public CreditLedgerService(
        BillingDbContext db,
        ILoggerManager logger,
        IOptions<BillingConcurrencyOptions> concurrencyOptions,
        IParentChildQuery parentChildQuery,
        ICurrentUserService currentUser,
        ISystemClock clock)
    {
        _db               = db;
        _logger           = logger;
        _concurrency      = concurrencyOptions.Value;
        _parentChildQuery = parentChildQuery;
        _currentUser      = currentUser;
        _clock            = clock;
    }

    // ── GrantFamilyWalletAsync ────────────────────────────────────────────────────

    /// <summary>
    /// Admin comp: deposits <paramref name="amount"/> into the shared family
    /// <c>FamilyEnergyAccount.PurchasedBalance</c> and writes an immutable ledger row.
    /// Creates the <c>FamilyEnergyAccount</c> if it does not yet exist.
    /// Idempotent per <paramref name="idempotencyKey"/>. Does NOT touch <c>CreditAccount</c>.
    /// </summary>
    public async Task<CreditLedgerResult> GrantFamilyWalletAsync(
        int parentId,
        int amount,
        string idempotencyKey,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Idempotency pre-check
        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.Duplicate();
        }

        // Resolve or create the family wallet
        var wallet = await _db.FamilyEnergyAccounts
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId, ct);

        if (wallet is null)
        {
            wallet = FamilyEnergyAccount.CreateEmpty(parentId);
            _db.FamilyEnergyAccounts.Add(wallet);
            await _db.SaveChangesAsync(actorUserId);
        }

        // Deposit into PurchasedBalance (permanent admin comp, never expires)
        wallet.PurchasedBalance += amount;

        // Write immutable ledger row
        var grantTx = new CreditTransaction
        {
            FamilyEnergyAccountId     = wallet.Id,
            Type                      = CreditTransactionType.Grant,
            Pool                      = CreditPool.Purchased,
            SourceBucket              = EnergyBucket.Purchased,
            Amount                    = amount,
            ReasonCode                = CreditReasonCode.AdminCredit,
            ResultingGrantedBalance   = wallet.SubscriptionBalance,
            ResultingPurchasedBalance = wallet.PurchasedBalance,
            OccurredAtUtc             = DateTime.UtcNow,
            IdempotencyKey            = idempotencyKey,
        };
        _db.CreditTransactions.Add(grantTx);

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        return CreditLedgerResult.Ok(wallet.PurchasedBalance + wallet.SubscriptionBalance, charged: false);
    }

    // ── SpendAsync (P10-13 CUTOVER: wallet-backed, no CreditAccount) ─────────────

    /// <summary>
    /// HTTP <c>POST /Credits/Spend</c> debit path. Debits the WALLET using the
    /// allocation-first → shared <c>FamilyEnergyAccount.PurchasedBalance</c>-fallback
    /// algorithm (mirrors <c>CreditSpendService.ExecuteDebitCoreAsync</c>).
    /// Does NOT touch <c>CreditAccount</c>.
    /// </summary>
    public async Task<CreditLedgerResult> SpendAsync(
        int childId,
        int amount,
        string reasonCode,
        string idempotencyKey,
        string? relatedActionId,
        int actorUserId,
        CancellationToken ct)
    {
        if (!Enum.TryParse<CreditReasonCode>(reasonCode, ignoreCase: true, out var reasonEnum))
            reasonEnum = CreditReasonCode.Unspecified;

        var maxRetries = _concurrency.MaxRetries;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await ExecuteWalletDebitAsync(childId, amount, reasonEnum, idempotencyKey, relatedActionId, actorUserId, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _logger.LogWarn($"CreditLedgerService: concurrency conflict attempt {attempt + 1} childId={childId}. Retrying.");
                _db.ChangeTracker.Clear();
                await ApplyBackoffDelayAsync(attempt, ct);
            }
            catch (DbUpdateException dbEx) when (IsIdempotencyKeyViolation(dbEx))
            {
                _logger.LogInfo($"CreditLedgerService: idempotent duplicate key={idempotencyKey}.");
                return await BuildIdempotentDebitResultAsync(idempotencyKey, ct);
            }
            catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx) && attempt < maxRetries)
            {
                _logger.LogWarn($"CreditLedgerService: unique-violation concurrency conflict attempt {attempt + 1} childId={childId}. Retrying.");
                _db.ChangeTracker.Clear();
                await ApplyBackoffDelayAsync(attempt, ct);
            }
        }

        _logger.LogError(new Exception("Retries exhausted"), $"CreditLedgerService: exceeded {maxRetries} retries childId={childId}");
        throw new InvalidOperationException($"CreditLedgerService: exceeded {maxRetries} retries for childId={childId}");
    }

    // ── ReconcileFamilyWalletAsync ─────────────────────────────────────────────────

    /// <summary>
    /// Recomputes the family wallet balances from the immutable ledger.
    /// Returns <c>null</c> if no <c>FamilyEnergyAccount</c> exists for the parent.
    /// Does NOT touch <c>CreditAccount</c>.
    /// </summary>
    public async Task<ReconciliationResultDto?> ReconcileFamilyWalletAsync(int parentId, CancellationToken ct)
    {
        var wallet = await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentId, ct);

        if (wallet is null) return null;

        // Fetch all ledger rows for this family wallet
        var transactions = await _db.CreditTransactions
            .AsNoTracking()
            .Where(t => t.FamilyEnergyAccountId == wallet.Id)
            .ToListAsync(ct);

        var ledgerSubscription = 0;
        var ledgerPurchased    = 0;

        foreach (var t in transactions)
        {
            switch (t.Type)
            {
                case CreditTransactionType.Grant:
                    if (t.SourceBucket == EnergyBucket.Subscription || t.Pool == CreditPool.Granted)
                        ledgerSubscription += t.Amount;
                    else
                        ledgerPurchased += t.Amount;
                    break;

                case CreditTransactionType.Expiry:
                    if (t.SourceBucket == EnergyBucket.Subscription || t.Pool == CreditPool.Granted)
                        ledgerSubscription -= t.Amount;
                    else
                        ledgerPurchased -= t.Amount;
                    break;

                case CreditTransactionType.Purchase:
                    ledgerPurchased += t.Amount;
                    break;

                case CreditTransactionType.Refund:
                    ledgerPurchased -= t.Amount;
                    break;

                case CreditTransactionType.Spend:
                    // Purchased-fallback spend (SourceBucket=Purchased) debits shared purchased pool.
                    if (t.SourceBucket == EnergyBucket.Purchased || t.Pool == CreditPool.Purchased)
                        ledgerPurchased -= t.Amount;
                    // Mixed guard rows (bare key) have full amount/fromGranted/fromPurchased
                    else if (t.FromPurchased > 0)
                        ledgerPurchased -= t.FromPurchased;
                    break;

                case CreditTransactionType.Adjustment:
                    // Admin credit (from GrantFamilyWalletAsync) lands in purchased pool
                    if (t.ReasonCode == CreditReasonCode.AdminCredit)
                        ledgerPurchased += t.Amount;
                    else
                        ledgerPurchased -= t.Amount;
                    break;
            }
        }

        return new ReconciliationResultDto
        {
            ParentId                   = parentId,
            StoredSubscriptionBalance  = wallet.SubscriptionBalance,
            StoredPurchasedBalance     = wallet.PurchasedBalance,
            LedgerSubscriptionBalance  = ledgerSubscription,
            LedgerPurchasedBalance     = ledgerPurchased,
            HasDrift                   = ledgerSubscription != wallet.SubscriptionBalance
                                         || ledgerPurchased != wallet.PurchasedBalance,
        };
    }

    // ── GetEnergyStatusAsync ──────────────────────────────────────────────────────

    /// <summary>
    /// P10-13 CUTOVER: reads the WALLET, not the legacy <c>CreditAccount</c>.
    /// </summary>
    public async Task<EnergyStatusDto> GetEnergyStatusAsync(
        int childId,
        IGlobalSettingsProvider settingsProvider,
        ISystemClock clock,
        CancellationToken ct)
    {
        var dailyCap         = settingsProvider.GetInt(GlobalSettingKeys.FreeDailyCap, 10);
        var monthlyAllotment = settingsProvider.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100);

        // Child's active allocation row (latest cycle) → granted (subscription) balance.
        var allocation = await _db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync(ct);

        // Shared family purchased balance
        FamilyEnergyAccount? wallet = null;
        if (allocation is not null)
        {
            wallet = await _db.FamilyEnergyAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == allocation.FamilyEnergyAccountId, ct);
        }
        else
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(childId, ct);
            if (parentId.HasValue)
                wallet = await _db.FamilyEnergyAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(w => w.ParentUserId == parentId.Value, ct);
        }

        var grantedBalance   = allocation?.Remaining ?? 0;
        var purchasedBalance = wallet?.PurchasedBalance ?? 0;
        var total            = grantedBalance + purchasedBalance;

        // Per-child daily usage (lazy-reset read)
        var dailyUsage = await _db.ChildDailyUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ChildId == childId, ct);

        var effectiveDailyUsed = dailyUsage is null
            || DailyCapHelper.IsStale(dailyUsage.DailyUsedDateLocal, dailyUsage.ChildTimeZoneId, clock)
                ? 0
                : dailyUsage.DailyUsed;

        var dailyCapReached = effectiveDailyUsed >= dailyCap;
        var approaching     = !dailyCapReached && effectiveDailyUsed >= (int)(dailyCap * 0.8);
        var lowThreshold    = (int)Math.Ceiling(monthlyAllotment * 0.10);
        var lowEnergy       = total > 0 && total <= lowThreshold;
        var empty           = total == 0;

        var warningState = empty
            ? WarningState.Empty
            : dailyCapReached
                ? WarningState.DailyCapReached
                : lowEnergy
                    ? WarningState.LowBalance
                    : approaching
                        ? WarningState.ApproachingDailyCap
                        : WarningState.None;

        return new EnergyStatusDto
        {
            GrantedBalance    = grantedBalance,
            PurchasedBalance  = purchasedBalance,
            TotalBalance      = total,
            GrantExpiresAtUtc = allocation?.CycleEndUtc,
            DailyUsed         = effectiveDailyUsed,
            DailyCap          = dailyCap,
            DailyCapReached   = dailyCapReached,
            LowEnergy         = lowEnergy,
            WarningState      = warningState,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Wallet debit (mirrors <c>CreditSpendService.ExecuteDebitCoreAsync</c>):
    /// allocation-first → shared <c>FamilyEnergyAccount.PurchasedBalance</c> fallback.
    /// Does NOT touch <c>CreditAccount</c>.
    /// </summary>
    private async Task<CreditLedgerResult> ExecuteWalletDebitAsync(
        int childId,
        int amount,
        CreditReasonCode reasonCode,
        string idempotencyKey,
        string? relatedActionId,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Idempotency pre-check
        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return await BuildIdempotentDebitResultAsync(idempotencyKey, ct);
        }

        // Load the child's active allocation row (latest cycle)
        var allocation = await _db.ChildEnergyAllocations
            .Where(a => a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync(ct);

        // Resolve the family wallet via the allocation row when present, else via the parent
        FamilyEnergyAccount? wallet;
        if (allocation is not null)
        {
            wallet = await _db.FamilyEnergyAccounts
                .FirstOrDefaultAsync(w => w.Id == allocation.FamilyEnergyAccountId, ct);
        }
        else
        {
            var parentId = await _parentChildQuery.FindParentForChildAsync(childId, ct);
            wallet = parentId.HasValue
                ? await _db.FamilyEnergyAccounts.FirstOrDefaultAsync(w => w.ParentUserId == parentId.Value, ct)
                : null;
        }

        var allocationRemaining = allocation?.Remaining ?? 0;
        var purchasedAvailable  = wallet?.PurchasedBalance ?? 0;

        if (allocation is null && wallet is null)
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.AccountNotFound();
        }

        var fromAllocation        = 0;
        var fromPurchasedFallback = 0;
        CreditTransaction? spendAllocTx       = null;
        CreditTransaction? spendPurchasedTx   = null;
        CreditTransaction? idempotencyGuardTx = null;

        if (allocation is not null && allocationRemaining >= amount)
        {
            fromAllocation = amount;
            spendAllocTx   = allocation.Debit(amount, reasonCode, idempotencyKey, relatedActionId);
        }
        else
        {
            var allocationCoverage = allocationRemaining;
            var shortfall          = amount - allocationCoverage;

            if (wallet is null || purchasedAvailable < shortfall)
            {
                await tx.RollbackAsync(ct);
                return CreditLedgerResult.InsufficientBalance();
            }

            fromAllocation        = allocationCoverage;
            fromPurchasedFallback = shortfall;

            if (allocationCoverage > 0 && allocation is not null)
            {
                idempotencyGuardTx = new CreditTransaction
                {
                    FamilyEnergyAccountId     = allocation.FamilyEnergyAccountId,
                    ChildEnergyAllocationId   = allocation.Id,
                    Type                      = CreditTransactionType.Spend,
                    Pool                      = CreditPool.Granted,
                    SourceBucket              = EnergyBucket.Subscription,
                    Amount                    = amount,
                    ReasonCode                = reasonCode,
                    FromGranted               = allocationCoverage,
                    FromPurchased             = shortfall,
                    ResultingGrantedBalance   = allocation.Remaining - allocationCoverage,
                    ResultingPurchasedBalance = wallet.PurchasedBalance - shortfall,
                    OccurredAtUtc             = DateTime.UtcNow,
                    IdempotencyKey            = idempotencyKey,
                    RelatedActionId           = relatedActionId ?? idempotencyKey,
                };

                var allocIdempotencyKey = $"{idempotencyKey}:alloc";
                spendAllocTx = allocation.Debit(allocationCoverage, reasonCode, allocIdempotencyKey, relatedActionId);
            }

            var purchasedIdempotencyKey = allocationCoverage > 0 ? $"{idempotencyKey}:purchased" : idempotencyKey;
            spendPurchasedTx = wallet.DebitPurchasedFallback(
                amount                 : shortfall,
                idempotencyKey         : purchasedIdempotencyKey,
                childEnergyAllocationId: allocation?.Id,
                relatedActionId        : relatedActionId ?? idempotencyKey);
        }

        if (idempotencyGuardTx is not null)
            _db.CreditTransactions.Add(idempotencyGuardTx);

        if (spendAllocTx is not null)
        {
            if (allocation?.FamilyEnergyAccountId > 0)
                spendAllocTx.FamilyEnergyAccountId = allocation.FamilyEnergyAccountId;
            _db.CreditTransactions.Add(spendAllocTx);
        }

        if (spendPurchasedTx is not null)
            _db.CreditTransactions.Add(spendPurchasedTx);

        // Daily usage increment
        var dailyUsage = await _db.ChildDailyUsages.FirstOrDefaultAsync(d => d.ChildId == childId, ct);
        if (dailyUsage is null)
        {
            dailyUsage = ChildDailyUsage.Create(childId);
            _db.ChildDailyUsages.Add(dailyUsage);
            await _db.SaveChangesAsync(actorUserId);
        }

        var todayLocal = DailyCapHelper.Today(dailyUsage.ChildTimeZoneId, _clock);
        if (DailyCapHelper.IsStale(dailyUsage.DailyUsedDateLocal, dailyUsage.ChildTimeZoneId, _clock))
            dailyUsage.DailyUsed = 0;
        dailyUsage.DailyUsed        += amount;
        dailyUsage.DailyUsedDateLocal = todayLocal;

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        var resultingTotal = (allocation?.Remaining ?? 0) + (wallet?.PurchasedBalance ?? 0);

        return CreditLedgerResult.Ok(
            resultingTotal,
            charged      : true,
            fromGranted  : fromAllocation,
            fromPurchased: fromPurchasedFallback);
    }

    private async Task<CreditLedgerResult> BuildIdempotentDebitResultAsync(string idempotencyKey, CancellationToken ct)
    {
        var prior = await _db.CreditTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

        return CreditLedgerResult.Duplicate(
            prior is not null
                ? prior.ResultingGrantedBalance + prior.ResultingPurchasedBalance
                : 0);
    }

    /// <summary>
    /// Applies exponential back-off + jitter before the next retry attempt.
    /// Called AFTER <c>ChangeTracker.Clear()</c> and AFTER the failed transaction's
    /// <c>await using</c> scope has exited. No lock is held during the delay.
    /// </summary>
    private Task ApplyBackoffDelayAsync(int attempt, CancellationToken ct)
    {
        var shift         = Math.Min(attempt, 30);
        var deterministic = (int)Math.Min(_concurrency.MaxDelayMs, (long)_concurrency.BaseDelayMs * (1L << shift));
        var jitter        = Random.Shared.Next(0, _concurrency.JitterMs + 1);
        var delayMs       = deterministic + jitter;
        return Task.Delay(delayMs, ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// True only for the CreditTransactions idempotency-key unique-constraint violation
    /// (<c>UX_CreditTransactions_IdempotencyKey</c>) — the single signal of an idempotent replay.
    /// </summary>
    private static bool IsIdempotencyKeyViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;
}
