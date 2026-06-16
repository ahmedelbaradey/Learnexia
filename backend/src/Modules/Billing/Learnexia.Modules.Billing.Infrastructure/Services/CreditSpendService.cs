using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Services;
using Learnexia.Modules.Billing.Infrastructure.Options;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Implements <see cref="ICreditSpendService"/> — the cross-module seam consumed by the
/// Ai module handlers (W2/P10-03) without referencing any <c>Billing.*</c> project.
///
/// <para><strong>Atomicity:</strong> <see cref="TryDebitAsync"/> opens an explicit transaction,
/// loads the account with the <c>xmin</c> concurrency token, checks idempotency, checks balance,
/// debits Granted-first, <strong>increments <c>DailyUsed</c> with lazy reset (P10-03/P10-04 W2b)</strong>,
/// inserts the ledger row, and commits — all in one transaction.
/// On <see cref="DbUpdateConcurrencyException"/> it retries up to <c>MaxRetries</c>.</para>
///
/// <para><strong>Idempotency:</strong> if the idempotency key already exists in
/// <c>CreditTransactions</c>, the prior result is returned without a second debit.</para>
///
/// <para><strong>Never-negative:</strong> the balance re-check inside the transaction ensures
/// the account is not over-drawn even under concurrent debits from multiple requests.</para>
///
/// <para><strong>Granted-first:</strong> <see cref="CreditAccount.GrantedBalance"/> is consumed
/// before <see cref="CreditAccount.PurchasedBalance"/>.</para>
///
/// <para><strong>Daily counter:</strong> <see cref="CreditAccount.DailyUsed"/> is incremented
/// inside the same transaction as the balance debit. When <see cref="CreditAccount.DailyUsedDateLocal"/>
/// is stale (different from child-local today), it is reset to 0 before incrementing. This is the
/// only write path for <c>DailyUsed</c> — <c>EnergyStatusQueryHandler</c> is read-only.</para>
/// </summary>
public class CreditSpendService : ICreditSpendService
{
    private readonly IBillingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IGlobalSettingsProvider _settings;
    private readonly ISystemClock _clock;
    private readonly BillingConcurrencyOptions _concurrency;

    public CreditSpendService(
        IBillingDbContext db,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IGlobalSettingsProvider settings,
        ISystemClock clock,
        IOptions<BillingConcurrencyOptions> concurrencyOptions)
    {
        _db          = db;
        _currentUser = currentUser;
        _logger      = logger;
        _settings    = settings;
        _clock       = clock;
        _concurrency = concurrencyOptions.Value;
    }

    /// <inheritdoc/>
    public async Task<DebitResult> TryDebitAsync(
        int childId,
        int amount,
        string reasonCode,
        string idempotencyKey,
        CancellationToken ct = default)
    {
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
                // Transaction from the failed attempt is already disposed/rolled back by
                // the `await using var tx` scope inside ExecuteDebitCoreAsync — safe to delay here.
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

        _logger.LogError(new Exception("Retries exhausted"), $"CreditSpendService: exceeded {maxRetries} retries childId={childId}");
        throw new InvalidOperationException($"CreditSpendService: exceeded {maxRetries} retries for childId={childId}");
    }

    /// <inheritdoc/>
    public async Task<EnergyBalance> GetBalanceAsync(int childId, CancellationToken ct = default)
    {
        var account = await _db.CreditAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == childId, ct);

        // Daily cap from GlobalSettings (free-tier default until P10-05 subscription tier).
        var dailyCap = _settings.GetInt(GlobalSettingKeys.FreeDailyCap, 10);

        if (account is null)
        {
            // Return zero balance — account will be created on first grant.
            return new EnergyBalance(
                GrantedBalance: 0,
                PurchasedBalance: 0,
                TotalBalance: 0,
                GrantExpiresAtUtc: null,
                DailyUsed: 0,
                DailyCap: dailyCap,
                DailyCapReached: false);
        }

        // Lazy daily-reset (read-side only — no write here; the write happens in TryDebitAsync).
        var effectiveDailyUsed = DailyCapHelper.IsStale(account.DailyUsedDateLocal, account.ChildTimeZoneId, _clock)
            ? 0
            : account.DailyUsed;

        return new EnergyBalance(
            GrantedBalance: account.GrantedBalance,
            PurchasedBalance: account.PurchasedBalance,
            TotalBalance: account.TotalBalance,
            GrantExpiresAtUtc: account.GrantExpiresAtUtc,
            DailyUsed: effectiveDailyUsed,
            DailyCap: dailyCap,
            DailyCapReached: effectiveDailyUsed >= dailyCap);
    }

    // ── Private helpers ───────────────────────────────────────────────────────────

    private async Task<DebitResult> ExecuteDebitCoreAsync(
        int childId,
        int amount,
        CreditReasonCode reasonCode,
        string idempotencyKey,
        CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Idempotency pre-check (cheaper than catching constraint violation).
        if (await _db.CreditTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct))
        {
            await tx.RollbackAsync(ct);
            return await BuildIdempotentResultAsync(idempotencyKey, ct);
        }

        var account = await _db.CreditAccounts
            .FirstOrDefaultAsync(a => a.ChildId == childId, ct);

        if (account is null || account.TotalBalance < amount)
        {
            await tx.RollbackAsync(ct);
            return new DebitResult(
                Charged: false,
                FromGranted: 0,
                FromPurchased: 0,
                ResultingTotal: account?.TotalBalance ?? 0,
                Outcome: DebitOutcome.InsufficientBalance);
        }

        // Granted-first split.
        var fromGranted = Math.Min(amount, account.GrantedBalance);
        var fromPurchased = amount - fromGranted;

        var creditTransaction = account.Debit(fromGranted, fromPurchased, reasonCode, idempotencyKey);
        await _db.CreditTransactions.AddAsync(creditTransaction, ct);

        // ── P10-03/P10-04 W2b: increment DailyUsed inside the same atomic transaction ──────────
        // Lazy reset: if DailyUsedDateLocal is stale (different from child-local today), reset to 0 first.
        var todayLocal = DailyCapHelper.Today(account.ChildTimeZoneId, _clock);
        if (DailyCapHelper.IsStale(account.DailyUsedDateLocal, account.ChildTimeZoneId, _clock))
        {
            account.DailyUsed = 0;
        }
        account.DailyUsed += amount;
        account.DailyUsedDateLocal = todayLocal;
        // ─────────────────────────────────────────────────────────────────────────────────────────

        await _db.SaveChangesAsync(_currentUser.UserId ?? 0);
        await tx.CommitAsync(ct);

        return new DebitResult(
            Charged: true,
            FromGranted: fromGranted,
            FromPurchased: fromPurchased,
            ResultingTotal: account.TotalBalance,
            Outcome: DebitOutcome.Charged);
    }

    private async Task<DebitResult> BuildIdempotentResultAsync(string idempotencyKey, CancellationToken ct)
    {
        var prior = await _db.CreditTransactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey, ct);

        return new DebitResult(
            Charged: true,
            FromGranted: 0,
            FromPurchased: 0,
            ResultingTotal: prior is not null
                ? prior.ResultingGrantedBalance + prior.ResultingPurchasedBalance
                : 0,
            Outcome: DebitOutcome.DuplicateIdempotent);
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
