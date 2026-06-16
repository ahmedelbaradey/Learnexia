using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Modules.Billing.Application.Features.EnergyStatus.Dtos;
using Learnexia.Modules.Billing.Application.Services;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Options;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ICreditLedgerService"/> (Option C).
///
/// <para>Owns ALL EF Core access, transaction management, idempotency, optimistic-concurrency
/// retry, and ledger logic for credit-account operations. Application-layer handlers inject
/// <see cref="ICreditLedgerService"/> and never reference EF or <c>BillingDbContext</c>.</para>
///
/// <para><strong>Optimistic-concurrency retry:</strong> debits load the account via the <c>xmin</c>
/// concurrency token. On <c>DbUpdateConcurrencyException</c> the handler retries up to
/// <c>MaxRetries</c> times (ChangeTracker cleared between attempts), with exponential
/// back-off and jitter between each retry. The back-off delay fires AFTER the failed
/// transaction is disposed/rolled back and AFTER <c>ChangeTracker.Clear()</c> — never
/// while a DB transaction or row lock is held.</para>
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

    public CreditLedgerService(
        BillingDbContext db,
        ILoggerManager logger,
        IOptions<BillingConcurrencyOptions> concurrencyOptions)
    {
        _db          = db;
        _logger      = logger;
        _concurrency = concurrencyOptions.Value;
    }

    // ── GrantAsync ───────────────────────────────────────────────────────────────

    public async Task<CreditLedgerResult> GrantAsync(
        int childId,
        int amount,
        DateTime expiresAtUtc,
        bool isPremium,
        string idempotencyKey,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.Duplicate();
        }

        var account = await _db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId, ct);
        if (account is null)
        {
            account = CreditAccount.CreateEmpty(childId);
            await _db.CreditAccounts.AddAsync(account, ct);
        }

        var reasonCode = isPremium ? CreditReasonCode.MonthlyGrantPremium : CreditReasonCode.MonthlyGrantFree;
        var creditTransaction = account.ApplyGrant(amount, expiresAtUtc, reasonCode, idempotencyKey);
        await _db.CreditTransactions.AddAsync(creditTransaction, ct);

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        return CreditLedgerResult.Ok(account.TotalBalance, charged: false);
    }

    // ── SpendAsync ───────────────────────────────────────────────────────────────

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
                return await ExecuteDebitAsync(childId, amount, reasonEnum, idempotencyKey, relatedActionId, actorUserId, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                // Transaction from the failed attempt is already disposed/rolled back by
                // the `await using var tx` scope inside ExecuteDebitAsync — safe to delay here.
                _logger.LogWarn($"CreditLedgerService: concurrency conflict attempt {attempt + 1} childId={childId}. Retrying.");
                _db.ChangeTracker.Clear();
                await ApplyBackoffDelayAsync(attempt, ct);
            }
            catch (DbUpdateException dbEx) when (IsUniqueViolation(dbEx))
            {
                _logger.LogInfo($"CreditLedgerService: idempotent duplicate key={idempotencyKey}.");
                return await BuildIdempotentDebitResultAsync(idempotencyKey, ct);
            }
        }

        _logger.LogError(new Exception("Retries exhausted"), $"CreditLedgerService: exceeded {maxRetries} retries childId={childId}");
        throw new InvalidOperationException($"CreditLedgerService: exceeded {maxRetries} retries for childId={childId}");
    }

    // ── RefundAsync ──────────────────────────────────────────────────────────────

    public async Task<CreditLedgerResult> RefundAsync(
        int childId,
        int amount,
        string idempotencyKey,
        string? relatedPaymentId,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.Duplicate();
        }

        var account = await _db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId, ct);
        if (account is null)
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.AccountNotFound();
        }

        var creditTransaction = account.Refund(amount, idempotencyKey, relatedPaymentId);
        await _db.CreditTransactions.AddAsync(creditTransaction, ct);

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        return CreditLedgerResult.Ok(account.TotalBalance);
    }

    // ── AdjustAsync ──────────────────────────────────────────────────────────────

    public async Task<CreditLedgerResult> AdjustAsync(
        int childId,
        int delta,
        string idempotencyKey,
        string? adminNote,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.Duplicate();
        }

        var account = await _db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId, ct);
        if (account is null)
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.AccountNotFound();
        }

        var creditTransaction = account.Adjust(delta, idempotencyKey, adminNote);
        await _db.CreditTransactions.AddAsync(creditTransaction, ct);

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        return CreditLedgerResult.Ok(account.TotalBalance);
    }

    // ── ExpireGrantAsync ─────────────────────────────────────────────────────────

    public async Task<CreditLedgerResult> ExpireGrantAsync(
        int childId,
        string idempotencyKey,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.Duplicate();
        }

        var account = await _db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId, ct);
        if (account is null)
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.AccountNotFound();
        }

        if (account.GrantedBalance == 0)
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.Ok(account.TotalBalance);
        }

        var creditTransaction = account.ExpireGrant(idempotencyKey);
        await _db.CreditTransactions.AddAsync(creditTransaction, ct);

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        return CreditLedgerResult.Ok(account.TotalBalance);
    }

    // ── ApplyPurchaseAsync ───────────────────────────────────────────────────────

    public async Task<CreditLedgerResult> ApplyPurchaseAsync(
        int childId,
        int amount,
        string idempotencyKey,
        string? relatedPaymentId,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.Duplicate();
        }

        var account = await _db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId, ct);
        if (account is null)
        {
            account = CreditAccount.CreateEmpty(childId);
            await _db.CreditAccounts.AddAsync(account, ct);
        }

        var creditTransaction = account.ApplyPurchase(amount, idempotencyKey, relatedPaymentId);
        await _db.CreditTransactions.AddAsync(creditTransaction, ct);

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        return CreditLedgerResult.Ok(account.TotalBalance, charged: false);
    }

    // ── GetCreditAccountAsync ─────────────────────────────────────────────────────

    public async Task<CreditAccountDto?> GetCreditAccountAsync(int childId, CancellationToken ct)
    {
        var account = await _db.CreditAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == childId, ct);

        if (account is null) return null;

        return new CreditAccountDto
        {
            ChildId          = account.ChildId,
            GrantedBalance   = account.GrantedBalance,
            PurchasedBalance = account.PurchasedBalance,
            TotalBalance     = account.TotalBalance,
            GrantExpiresAtUtc = account.GrantExpiresAtUtc,
            DailyUsed        = account.DailyUsed,
        };
    }

    // ── ReconcileAsync ────────────────────────────────────────────────────────────

    public async Task<ReconciliationResultDto?> ReconcileAsync(int childId, CancellationToken ct)
    {
        var account = await _db.CreditAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == childId, ct);

        if (account is null) return null;

        var transactions = await _db.CreditTransactions
            .AsNoTracking()
            .Where(t => t.CreditAccountId == account.Id)
            .ToListAsync(ct);

        var ledgerGranted   = 0;
        var ledgerPurchased = 0;

        foreach (var t in transactions)
        {
            switch (t.Type)
            {
                case CreditTransactionType.Grant:
                    ledgerGranted += t.Amount;
                    break;
                case CreditTransactionType.Expiry:
                    ledgerGranted -= t.Amount;
                    break;
                case CreditTransactionType.Purchase:
                    ledgerPurchased += t.Amount;
                    break;
                case CreditTransactionType.Refund:
                    ledgerPurchased -= t.Amount;
                    break;
                case CreditTransactionType.Spend:
                    if (t.FromGranted > 0 || t.FromPurchased > 0)
                    {
                        ledgerGranted   -= t.FromGranted;
                        ledgerPurchased -= t.FromPurchased;
                    }
                    else if (t.Pool == CreditPool.Granted)   ledgerGranted   -= t.Amount;
                    else if (t.Pool == CreditPool.Purchased) ledgerPurchased -= t.Amount;
                    else
                    {
                        ledgerGranted -= t.ResultingGrantedBalance > 0
                            ? Math.Min(t.Amount, t.ResultingGrantedBalance)
                            : 0;
                        ledgerPurchased -= t.Amount - (t.ResultingGrantedBalance > 0
                            ? Math.Min(t.Amount, t.ResultingGrantedBalance)
                            : 0);
                    }
                    break;
                case CreditTransactionType.Adjustment:
                    if (t.ReasonCode == CreditReasonCode.AdminCredit) ledgerGranted += t.Amount;
                    else                                               ledgerGranted -= t.Amount;
                    break;
            }
        }

        return new ReconciliationResultDto
        {
            ChildId                 = childId,
            StoredGrantedBalance    = account.GrantedBalance,
            StoredPurchasedBalance  = account.PurchasedBalance,
            LedgerGrantedBalance    = ledgerGranted,
            LedgerPurchasedBalance  = ledgerPurchased,
            HasDrift                = ledgerGranted != account.GrantedBalance || ledgerPurchased != account.PurchasedBalance,
        };
    }

    // ── GetEnergyStatusAsync ──────────────────────────────────────────────────────

    public async Task<EnergyStatusDto> GetEnergyStatusAsync(
        int childId,
        IGlobalSettingsProvider settingsProvider,
        ISystemClock clock,
        CancellationToken ct)
    {
        var account = await _db.CreditAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == childId, ct);

        var dailyCap         = settingsProvider.GetInt(GlobalSettingKeys.FreeDailyCap, 10);
        var monthlyAllotment = settingsProvider.GetInt(GlobalSettingKeys.FreeMonthlyCredits, 100);

        if (account is null)
        {
            return new EnergyStatusDto
            {
                GrantedBalance   = 0,
                PurchasedBalance = 0,
                TotalBalance     = 0,
                GrantExpiresAtUtc = null,
                DailyUsed        = 0,
                DailyCap         = dailyCap,
                DailyCapReached  = false,
                LowEnergy        = false,
                WarningState     = WarningState.None,
            };
        }

        var effectiveDailyUsed = DailyCapHelper.IsStale(account.DailyUsedDateLocal, account.ChildTimeZoneId, clock)
            ? 0
            : account.DailyUsed;

        var dailyCapReached = effectiveDailyUsed >= dailyCap;
        var approaching     = !dailyCapReached && effectiveDailyUsed >= (int)(dailyCap * 0.8);
        var total           = account.TotalBalance;
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
            GrantedBalance   = account.GrantedBalance,
            PurchasedBalance = account.PurchasedBalance,
            TotalBalance     = total,
            GrantExpiresAtUtc = account.GrantExpiresAtUtc,
            DailyUsed        = effectiveDailyUsed,
            DailyCap         = dailyCap,
            DailyCapReached  = dailyCapReached,
            LowEnergy        = lowEnergy,
            WarningState     = warningState,
        };
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<CreditLedgerResult> ExecuteDebitAsync(
        int childId,
        int amount,
        CreditReasonCode reasonCode,
        string idempotencyKey,
        string? relatedActionId,
        int actorUserId,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var account = await _db.CreditAccounts.FirstOrDefaultAsync(a => a.ChildId == childId, ct);

        if (account is null)
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.AccountNotFound();
        }

        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return await BuildIdempotentDebitResultAsync(idempotencyKey, ct);
        }

        if (account.TotalBalance < amount)
        {
            await tx.RollbackAsync(ct);
            return CreditLedgerResult.InsufficientBalance();
        }

        var fromGranted   = Math.Min(amount, account.GrantedBalance);
        var fromPurchased = amount - fromGranted;

        var creditTransaction = account.Debit(fromGranted, fromPurchased, reasonCode, idempotencyKey, relatedActionId);
        await _db.CreditTransactions.AddAsync(creditTransaction, ct);

        await _db.SaveChangesAsync(actorUserId);
        await tx.CommitAsync(ct);

        return CreditLedgerResult.Ok(account.TotalBalance, charged: true, fromGranted: fromGranted, fromPurchased: fromPurchased);
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
    ///
    /// <para>Formula: delay = min(MaxDelayMs, BaseDelayMs * 2^attempt) + Random.Shared.Next(0, JitterMs + 1)</para>
    ///
    /// <para>Called AFTER <c>ChangeTracker.Clear()</c> and AFTER the failed transaction's
    /// <c>await using</c> scope has exited (disposed → rolled back). No lock is held
    /// during the delay.</para>
    /// </summary>
    private Task ApplyBackoffDelayAsync(int attempt, CancellationToken ct)
    {
        // Clamp the shift exponent + use long arithmetic so a misconfigured MaxRetries (>= 31)
        // can never overflow the back-off computation; the delay stays bounded by MaxDelayMs.
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
}
