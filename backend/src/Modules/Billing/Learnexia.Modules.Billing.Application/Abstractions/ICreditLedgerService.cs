using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Modules.Billing.Application.Features.EnergyStatus.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for all credit-ledger operations on a child's <c>CreditAccount</c>.
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface,
/// not <see cref="IBillingDbContext"/>. All EF, transaction, idempotency, optimistic-concurrency,
/// and ledger logic lives in the Infrastructure implementation. Handlers stay EF-free.</para>
///
/// <para><strong>Invariants preserved inside the implementation:</strong>
/// <list type="bullet">
///   <item>Every write opens an explicit DB transaction — guarantees atomicity for
///         idempotency-check → mutate → commit sequences.</item>
///   <item>Idempotency keys checked via <c>CreditTransactions.AnyAsync</c> BEFORE the insert,
///         and <c>UX_CreditTransactions_IdempotencyKey</c> unique constraint catches concurrent
///         races.</item>
///   <item><c>CreditAccount.Version</c> (xmin) provides optimistic concurrency on debits;
///         the implementation retries up to <c>MaxRetries</c> on
///         <c>DbUpdateConcurrencyException</c>.</item>
///   <item>Granted-first split: <c>GrantedBalance</c> is consumed before <c>PurchasedBalance</c>
///         on every debit.</item>
///   <item>Never-negative: domain entity mutators clamp to available balance.</item>
/// </list>
/// </para>
/// </summary>
public interface ICreditLedgerService
{
    /// <summary>
    /// Grants credits to the child's account (monthly-grant path).
    /// Creates the account if it does not yet exist. Idempotent per <paramref name="idempotencyKey"/>.
    /// </summary>
    Task<CreditLedgerResult> GrantAsync(
        int childId,
        int amount,
        DateTime expiresAtUtc,
        bool isPremium,
        string idempotencyKey,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Atomically debits credits from the child's account with optimistic-concurrency retry.
    /// Returns <see cref="CreditLedgerOutcome.InsufficientBalance"/> if the balance is too low.
    /// Returns <see cref="CreditLedgerOutcome.DuplicateIdempotent"/> if the key was already processed.
    /// </summary>
    Task<CreditLedgerResult> SpendAsync(
        int childId,
        int amount,
        string reasonCode,
        string idempotencyKey,
        string? relatedActionId,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Refunds credits to the purchased pool. Idempotent per <paramref name="idempotencyKey"/>.
    /// </summary>
    Task<CreditLedgerResult> RefundAsync(
        int childId,
        int amount,
        string idempotencyKey,
        string? relatedPaymentId,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Admin adjustment: positive delta adds to granted pool, negative reduces granted pool.
    /// Idempotent per <paramref name="idempotencyKey"/>.
    /// </summary>
    Task<CreditLedgerResult> AdjustAsync(
        int childId,
        int delta,
        string idempotencyKey,
        string? adminNote,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Expires any remaining granted balance on the child's account.
    /// No-op if granted balance is already zero. Idempotent per <paramref name="idempotencyKey"/>.
    /// </summary>
    Task<CreditLedgerResult> ExpireGrantAsync(
        int childId,
        string idempotencyKey,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Applies a pack purchase to the child's purchased-pool balance.
    /// Creates the account if it does not yet exist. Idempotent per <paramref name="idempotencyKey"/>.
    /// </summary>
    Task<CreditLedgerResult> ApplyPurchaseAsync(
        int childId,
        int amount,
        string idempotencyKey,
        string? relatedPaymentId,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Returns the credit account balance snapshot for a child.
    /// Returns <c>null</c> if no account exists for the child.
    /// </summary>
    Task<CreditAccountDto?> GetCreditAccountAsync(int childId, CancellationToken ct);

    /// <summary>
    /// Reconciles the stored account balances against the full ledger.
    /// Returns <c>null</c> if no account exists for the child.
    /// </summary>
    Task<ReconciliationResultDto?> ReconcileAsync(int childId, CancellationToken ct);

    /// <summary>
    /// Returns the energy status (balances, daily-cap state, warning state) for a child.
    /// Uses <paramref name="settingsProvider"/> to resolve cap/allotment settings.
    /// The daily-cap reset is lazy (read-only path — no DB write here).
    /// </summary>
    Task<EnergyStatusDto> GetEnergyStatusAsync(
        int childId,
        IGlobalSettingsProvider settingsProvider,
        ISystemClock clock,
        CancellationToken ct);
}

/// <summary>Outcome codes for <see cref="ICreditLedgerService"/> operations.</summary>
public enum CreditLedgerOutcome
{
    /// <summary>The operation completed successfully.</summary>
    Succeeded = 0,

    /// <summary>The idempotency key was already processed — result is the prior outcome.</summary>
    DuplicateIdempotent = 1,

    /// <summary>The account does not have sufficient total balance for the debit.</summary>
    InsufficientBalance = 2,

    /// <summary>No credit account exists for the specified child.</summary>
    AccountNotFound = 3,
}

/// <summary>Result of a <see cref="ICreditLedgerService"/> write operation.</summary>
public sealed record CreditLedgerResult
{
    public CreditLedgerOutcome Outcome { get; init; }
    public bool Charged { get; init; }
    public int FromGranted { get; init; }
    public int FromPurchased { get; init; }
    public int ResultingTotal { get; init; }

    public static CreditLedgerResult Ok(int resultingTotal, bool charged = true, int fromGranted = 0, int fromPurchased = 0)
        => new() { Outcome = CreditLedgerOutcome.Succeeded, Charged = charged, ResultingTotal = resultingTotal, FromGranted = fromGranted, FromPurchased = fromPurchased };

    public static CreditLedgerResult Duplicate(int resultingTotal = 0)
        => new() { Outcome = CreditLedgerOutcome.DuplicateIdempotent, Charged = true, ResultingTotal = resultingTotal };

    public static CreditLedgerResult InsufficientBalance()
        => new() { Outcome = CreditLedgerOutcome.InsufficientBalance };

    public static CreditLedgerResult AccountNotFound()
        => new() { Outcome = CreditLedgerOutcome.AccountNotFound };
}
