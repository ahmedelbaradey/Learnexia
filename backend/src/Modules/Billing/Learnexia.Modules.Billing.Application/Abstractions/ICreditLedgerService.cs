using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Modules.Billing.Application.Features.EnergyStatus.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for credit-ledger operations on the FAMILY WALLET.
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface,
/// not <see cref="IBillingDbContext"/>. All EF, transaction, idempotency, and ledger logic
/// lives in the Infrastructure implementation. Handlers stay EF-free.</para>
///
/// <para><strong>CreditAccount RETIRED:</strong> All per-child <c>CreditAccount</c> paths
/// (ApplyPurchase, per-child Refund, Adjust, ExpireGrant, GetCreditAccount) have been removed.
/// Energy movement goes through the Family Wallet + the immutable <c>CreditTransaction</c> ledger.</para>
///
/// <para><strong>Retained active methods:</strong>
/// <list type="bullet">
///   <item><see cref="GrantFamilyWalletAsync"/> — admin comp deposit to the family <c>PurchasedBalance</c>.</item>
///   <item><see cref="SpendAsync"/> — wallet debit (allocation-first → purchased fallback).</item>
///   <item><see cref="ReconcileFamilyWalletAsync"/> — recompute family wallet from the ledger.</item>
///   <item><see cref="GetEnergyStatusAsync"/> — per-child energy status from the wallet (no CreditAccount).</item>
/// </list>
/// </para>
/// </summary>
public interface ICreditLedgerService
{
    /// <summary>
    /// Admin comp: deposits <paramref name="amount"/> into the shared family
    /// <c>FamilyEnergyAccount.PurchasedBalance</c> (permanent admin comp) and writes an immutable ledger row.
    /// Creates the <c>FamilyEnergyAccount</c> if it does not yet exist for the parent.
    /// Idempotent per <paramref name="idempotencyKey"/>.
    /// Does NOT touch <c>CreditAccount</c>.
    /// </summary>
    Task<CreditLedgerResult> GrantFamilyWalletAsync(
        int parentId,
        int amount,
        string idempotencyKey,
        int actorUserId,
        CancellationToken ct);

    /// <summary>
    /// Atomically debits energy from the child's family wallet (allocation-first →
    /// shared <c>FamilyEnergyAccount.PurchasedBalance</c> fallback) with optimistic-concurrency retry.
    /// Returns <see cref="CreditLedgerOutcome.InsufficientBalance"/> if the balance is too low.
    /// Returns <see cref="CreditLedgerOutcome.DuplicateIdempotent"/> if the key was already processed.
    /// Does NOT touch <c>CreditAccount</c>.
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
    /// Recomputes the family wallet balances (<c>SubscriptionBalance</c> and <c>PurchasedBalance</c>)
    /// from the immutable <c>CreditTransaction</c> ledger for the given <paramref name="parentId"/>.
    /// Reports drift but does NOT auto-heal.
    /// Returns <c>null</c> if no <c>FamilyEnergyAccount</c> exists for the parent.
    /// Does NOT touch <c>CreditAccount</c>.
    /// </summary>
    Task<ReconciliationResultDto?> ReconcileFamilyWalletAsync(int parentId, CancellationToken ct);

    /// <summary>
    /// Returns the energy status (balances, daily-cap state, warning state) for a child.
    /// Reads from the family wallet — no <c>CreditAccount</c> involved.
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

    /// <summary>No family wallet exists for the specified parent.</summary>
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
