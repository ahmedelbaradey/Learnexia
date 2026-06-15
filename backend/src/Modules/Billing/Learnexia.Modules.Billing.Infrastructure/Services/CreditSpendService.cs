using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Implements <see cref="ICreditSpendService"/> — the cross-module seam consumed by the
/// Ai module handlers (W2/P10-03) without referencing any <c>Billing.*</c> project.
///
/// <para><strong>Atomicity:</strong> <see cref="TryDebitAsync"/> opens an explicit transaction,
/// loads the account with the <c>xmin</c> concurrency token, checks idempotency, checks balance,
/// debits Granted-first, inserts the ledger row, and commits — all in one transaction.
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
/// </summary>
public class CreditSpendService : ICreditSpendService
{
    private const int MaxRetries = 3;

    private readonly IBillingDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;

    public CreditSpendService(IBillingDbContext db, ICurrentUserService currentUser, ILoggerManager logger)
    {
        _db = db;
        _currentUser = currentUser;
        _logger = logger;
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

        for (var attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                return await ExecuteDebitCoreAsync(childId, amount, reasonEnum, idempotencyKey, ct);
            }
            catch (DbUpdateConcurrencyException) when (attempt < MaxRetries)
            {
                _logger.LogWarn($"CreditSpendService: concurrency conflict attempt {attempt + 1} childId={childId}. Retrying.");
                _db.ChangeTracker.Clear();
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

        _logger.LogError(new Exception("Retries exhausted"), $"CreditSpendService: exceeded {MaxRetries} retries childId={childId}");
        throw new InvalidOperationException($"CreditSpendService: exceeded {MaxRetries} retries for childId={childId}");
    }

    /// <inheritdoc/>
    public async Task<EnergyBalance> GetBalanceAsync(int childId, CancellationToken ct = default)
    {
        var account = await _db.CreditAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.ChildId == childId, ct);

        if (account is null)
        {
            // Return zero balance — account will be created on first grant.
            return new EnergyBalance(0, 0, 0, null);
        }

        return new EnergyBalance(
            account.GrantedBalance,
            account.PurchasedBalance,
            account.TotalBalance,
            account.GrantExpiresAtUtc);
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

    private static bool IsUniqueViolation(DbUpdateException ex)
        => ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
           || ex.InnerException?.Message.Contains("UX_CreditTransactions_IdempotencyKey",
               StringComparison.OrdinalIgnoreCase) == true;
}
