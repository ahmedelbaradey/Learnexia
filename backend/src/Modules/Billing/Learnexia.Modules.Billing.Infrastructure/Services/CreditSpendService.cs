using Learnexia.Modules.Billing.Application.Services;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Options;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Re-implementation of <see cref="ICreditSpendService"/> for the P10-13 family-wallet model.
///
/// <para><strong>Algorithm (allocation-first → purchased-fallback):</strong>
/// <list type="number">
///   <item>Idempotency pre-check.</item>
///   <item>Load the child's active <c>ChildEnergyAllocation</c> row (with <c>xmin</c>).</item>
///   <item>Debit the allocation row first (only the child's own allowance is touched).</item>
///   <item>If <c>Remaining &lt; amount</c>, draw the shortfall from the shared family
///         <c>FamilyEnergyAccount.PurchasedBalance</c> (fallback path — the shared row is
///         NOT read or locked on the normal allocation-covered path).</item>
///   <item>If neither covers it, return typed <c>InsufficientBalance</c> (no DB write).</item>
///   <item>Increment the per-child <c>ChildDailyUsage</c> row (lazy reset) inside the same txn (OQ-G).</item>
///   <item>Write <c>SpendAllocation</c> and/or <c>SpendPurchasedFallback</c> ledger rows.</item>
///   <item>Commit. On <c>DbUpdateConcurrencyException</c> retry with exponential back-off (HARDEN-01).</item>
/// </list>
/// </para>
///
/// <para><strong>GetBalanceAsync:</strong> returns derived totals from the child's active allocation
/// row + shared purchased balance. Creates the <c>ChildDailyUsage</c> row on first use.</para>
///
/// <para><strong>AI module compatibility:</strong> the signature of <see cref="TryDebitAsync"/> is
/// FROZEN. <see cref="DebitResult.FromGranted"/> and <see cref="FromPurchased"/> in the result are
/// populated to avoid breaking Ai handler code that reads those fields (they map to
/// <c>FromAllocation</c> and <c>FromPurchasedFallback</c> respectively). The new init-only
/// <c>FromAllocation</c> / <c>FromPurchasedFallback</c> carry the wallet semantics.</para>
/// </summary>
public class CreditSpendService : ICreditSpendService
{
    private readonly BillingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ISystemClock _clock;
    private readonly BillingConcurrencyOptions _concurrency;
    private readonly ISeatStateQuery _seatStateQuery;

    public CreditSpendService(
        BillingDbContext db,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IGlobalSettingsProvider settings,
        ISystemClock clock,
        IOptions<BillingConcurrencyOptions> concurrencyOptions,
        ISeatStateQuery seatStateQuery)
    {
        _db             = db;
        _currentUser    = currentUser;
        _logger         = logger;
        _settings       = settings;
        _clock          = clock;
        _concurrency    = concurrencyOptions.Value;
        _seatStateQuery = seatStateQuery;
    }

    // ── TryDebitAsync ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<DebitResult> TryDebitAsync(
        int childId,
        int amount,
        string reasonCode,
        string idempotencyKey,
        CancellationToken ct = default)
    {
        // ── P10-15-BE-5: Seat-state gate ─────────────────────────────────────────────
        // Deny spend BEFORE any balance is touched when the child's seat is NoSeatLocked.
        // Purchased (pack) energy is NEVER touched in this path.
        if (!await _seatStateQuery.IsChildSeatActiveAsync(childId, ct))
        {
            _logger.LogInfo($"CreditSpendService: childId={childId} seat is NoSeatLocked — spend denied.");
            return new DebitResult(
                Charged       : false,
                FromGranted   : 0,
                FromPurchased : 0,
                ResultingTotal: 0,
                Outcome       : DebitOutcome.SeatLocked);
        }

        if (!Enum.TryParse<CreditReasonCode>(reasonCode, ignoreCase: true, out var reasonEnum))
            reasonEnum = CreditReasonCode.Unspecified;

        var maxRetries = _concurrency.MaxRetries;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await ExecuteDebitCoreAsync(childId, amount, reasonEnum, idempotencyKey, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _logger.LogWarn($"CreditSpendService: concurrency conflict attempt {attempt + 1} childId={childId}. Retrying.");
                _db.ChangeTracker.Clear();
                await ApplyBackoffDelayAsync(attempt, ct);
            }
            catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
            {
                _logger.LogInfo($"CreditSpendService: idempotent duplicate key={idempotencyKey}.");
                return await BuildIdempotentResultAsync(idempotencyKey, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"CreditSpendService: error in TryDebitAsync childId={childId}");
                throw;
            }
        }

        _logger.LogError(new Exception("Retries exhausted"), $"CreditSpendService: exceeded {_concurrency.MaxRetries} retries childId={childId}");
        throw new InvalidOperationException($"CreditSpendService: exceeded {_concurrency.MaxRetries} retries for childId={childId}");
    }

    // ── GetBalanceAsync ───────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<EnergyBalance> GetBalanceAsync(int childId, CancellationToken ct = default)
    {
        var dailyCap = _settings.GetInt(GlobalSettingKeys.FreeDailyCap, 10);

        // Find the child's active allocation row (latest cycle).
        var allocation = await _db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync(ct);

        // Find the family purchased balance via the wallet.
        int purchasedBalance = 0;
        if (allocation is not null)
        {
            var wallet = await _db.FamilyEnergyAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == allocation.FamilyEnergyAccountId, ct);
            purchasedBalance = wallet?.PurchasedBalance ?? 0;
        }

        // Daily usage row.
        var dailyUsage = await _db.ChildDailyUsages
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.ChildId == childId, ct);

        var effectiveDailyUsed = dailyUsage is null || DailyCapHelper.IsStale(dailyUsage.DailyUsedDateLocal, dailyUsage.ChildTimeZoneId, _clock)
            ? 0
            : dailyUsage.DailyUsed;

        var grantedBalance  = allocation?.Remaining ?? 0;
        var totalBalance    = grantedBalance + purchasedBalance;

        return new EnergyBalance(
            GrantedBalance      : grantedBalance,
            PurchasedBalance    : purchasedBalance,
            TotalBalance        : totalBalance,
            GrantExpiresAtUtc   : allocation?.CycleEndUtc,
            DailyUsed           : effectiveDailyUsed,
            DailyCap            : dailyCap,
            DailyCapReached     : effectiveDailyUsed >= dailyCap);
    }

    // ── Core debit logic ──────────────────────────────────────────────────────────

    private async Task<DebitResult> ExecuteDebitCoreAsync(
        int childId,
        int amount,
        CreditReasonCode reasonCode,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // ── Idempotency pre-check ──────────────────────────────────────────────────
        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return await BuildIdempotentResultAsync(idempotencyKey, ct);
        }

        // ── Load the child's active allocation row (xmin for OCC) ─────────────────
        var allocation = await _db.ChildEnergyAllocations
            .Where(a => a.ChildId == childId)
            .OrderByDescending(a => a.CycleStartUtc)
            .FirstOrDefaultAsync(ct);

        var fromAllocation      = 0;
        var fromPurchasedFallback = 0;
        CreditTransaction? spendAllocTx = null;
        CreditTransaction? spendPurchasedTx = null;
        // Guard row persisted under the BARE idempotencyKey for every spend shape so that
        // the pre-check (AnyAsync on bare key) fires correctly on retry (SECURITY HIGH-1).
        // On alloc-only path: the main ledger row already uses the bare key — guard not needed.
        // On purchased-only path: same, purchased row uses the bare key — guard not needed.
        // On mixed path: alloc row uses "{key}:alloc", purchased row uses "{key}:purchased" —
        //   a guard row under the bare key MUST be inserted so retries are blocked.
        CreditTransaction? idempotencyGuardTx = null;

        if (allocation is not null && allocation.Remaining >= amount)
        {
            // Normal path: allocation row covers the full cost — shared purchased row stays COLD.
            // The ledger row uses the bare idempotencyKey; no separate guard needed.
            fromAllocation = amount;
            spendAllocTx = allocation.Debit(amount, reasonCode, idempotencyKey);
        }
        else
        {
            // Shortfall path or no allocation: determine how much the allocation can cover.
            var allocationCoverage = allocation?.Remaining ?? 0;
            var shortfall          = amount - allocationCoverage;

            // Load the family wallet for the purchased-fallback debit (only NOW on shortfall).
            var walletId = allocation?.FamilyEnergyAccountId;
            FamilyEnergyAccount? wallet = walletId.HasValue
                ? await _db.FamilyEnergyAccounts.FirstOrDefaultAsync(w => w.Id == walletId.Value, ct)
                : null;

            if (wallet is null || wallet.PurchasedBalance < shortfall)
            {
                // Neither covers it — no write.
                await tx.RollbackAsync(ct);
                var resultingTotal = (allocation?.Remaining ?? 0) + (wallet?.PurchasedBalance ?? 0);
                return new DebitResult(
                    Charged       : false,
                    FromGranted   : 0,
                    FromPurchased : 0,
                    ResultingTotal: resultingTotal,
                    Outcome       : DebitOutcome.InsufficientBalance);
            }

            // Debit allocation for whatever it has, then shortfall from purchased.
            fromAllocation        = allocationCoverage;
            fromPurchasedFallback = shortfall;

            if (allocationCoverage > 0 && allocation is not null)
            {
                // MIXED spend: allocation covers part, purchased covers shortfall.
                // Persist a guard row under the BARE key first, so both the pre-check AND
                // the DB unique constraint protect against double-debit on retry.
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
                    ResultingGrantedBalance   = allocation.Remaining - allocationCoverage, // post-debit
                    ResultingPurchasedBalance = wallet.PurchasedBalance - shortfall,       // post-debit
                    OccurredAtUtc             = DateTime.UtcNow,
                    IdempotencyKey            = idempotencyKey,
                    RelatedActionId           = idempotencyKey,
                };

                var allocIdempotencyKey = $"{idempotencyKey}:alloc";
                spendAllocTx = allocation.Debit(allocationCoverage, reasonCode, allocIdempotencyKey);
            }

            // purchased-only (allocationCoverage == 0): use bare key on the purchased row directly.
            var purchasedIdempotencyKey = allocationCoverage > 0 ? $"{idempotencyKey}:purchased" : idempotencyKey;
            spendPurchasedTx = wallet.DebitPurchasedFallback(
                amount         : shortfall,
                idempotencyKey : purchasedIdempotencyKey,
                childEnergyAllocationId: allocation?.Id,
                relatedActionId: idempotencyKey);
        }

        // ── Write ledger rows ──────────────────────────────────────────────────────
        // Guard row first (mixed path only) — hits the unique constraint on retry before
        // any accounting rows are written.
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

        // ── Daily usage increment (OQ-G — per-child row, survives allocation reset) ──
        var dailyUsage = await _db.ChildDailyUsages.FirstOrDefaultAsync(d => d.ChildId == childId, ct);
        if (dailyUsage is null)
        {
            dailyUsage = ChildDailyUsage.Create(childId);
            _db.ChildDailyUsages.Add(dailyUsage);
            await _db.SaveChangesAsync(_currentUser.UserId ?? 0); // flush to get Id
        }

        var todayLocal = DailyCapHelper.Today(dailyUsage.ChildTimeZoneId, _clock);
        if (DailyCapHelper.IsStale(dailyUsage.DailyUsedDateLocal, dailyUsage.ChildTimeZoneId, _clock))
            dailyUsage.DailyUsed = 0;
        dailyUsage.DailyUsed        += amount;
        dailyUsage.DailyUsedDateLocal = todayLocal;

        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
        await tx.CommitAsync(ct);

        // Compute resulting total for the result.
        var resultingAllocationRemaining  = allocation?.Remaining ?? 0;
        var walletForResult               = spendPurchasedTx is not null
            ? await _db.FamilyEnergyAccounts.AsNoTracking()
                .FirstOrDefaultAsync(w => w.Id == allocation!.FamilyEnergyAccountId, ct)
            : null;
        var resultingPurchasedBalance = walletForResult?.PurchasedBalance ?? 0;
        var resultingTotalBalance     = resultingAllocationRemaining + resultingPurchasedBalance;

        return new DebitResult(
            Charged       : true,
            FromGranted   : fromAllocation,         // Ai handlers read .FromGranted — maps to allocation
            FromPurchased : fromPurchasedFallback,  // Ai handlers read .FromPurchased — maps to fallback
            ResultingTotal: resultingTotalBalance,
            Outcome       : DebitOutcome.Charged)
        {
            FromAllocation        = fromAllocation,
            FromPurchasedFallback = fromPurchasedFallback,
        };
    }

    private async Task<DebitResult> BuildIdempotentResultAsync(string idempotencyKey, CancellationToken ct)
    {
        var prior = await _db.CreditTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

        return new DebitResult(
            Charged       : true,
            FromGranted   : 0,
            FromPurchased : 0,
            ResultingTotal: prior is not null
                ? prior.ResultingGrantedBalance + prior.ResultingPurchasedBalance
                : 0,
            Outcome: DebitOutcome.DuplicateIdempotent);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>HARDEN-01: exponential back-off with jitter; clamped exponent; long arithmetic.</summary>
    private Task ApplyBackoffDelayAsync(int attempt, CancellationToken ct)
    {
        var shift         = Math.Min(attempt, 30);
        var deterministic = (int)Math.Min(_concurrency.MaxDelayMs, (long)_concurrency.BaseDelayMs * (1L << shift));
        var jitter        = Random.Shared.Next(0, _concurrency.JitterMs + 1);
        return Task.Delay(deterministic + jitter, ct);
    }

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException is Npgsql.PostgresException pg
           && pg.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation;
}
