namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Cross-module seam that lets the Ai module debit and query energy credits without
/// referencing any <c>Billing.*</c> project (module isolation rule 1).
///
/// <para>Implemented in <c>Billing.Infrastructure</c> as <c>CreditSpendService</c>.
/// Consumed by the 4 Ai handlers in Wave 2 (P10-03).</para>
///
/// <para>Key invariants enforced by the implementation:
/// <list type="bullet">
///   <item>Atomic: balance check + decrement + ledger insert happen in ONE explicit transaction.</item>
///   <item>Idempotent: same <paramref name="idempotencyKey"/> → no double-debit; returns prior result.</item>
///   <item>Never-negative: TotalBalance never goes below zero under concurrency (xmin retry).</item>
///   <item>Granted-first: GrantedBalance is drawn down before PurchasedBalance.</item>
/// </list>
/// </para>
/// </summary>
public interface ICreditSpendService
{
    /// <summary>
    /// Attempts to debit <paramref name="amount"/> credits from the child's account.
    ///
    /// <para>Returns <see cref="DebitOutcome.Charged"/> on success,
    /// <see cref="DebitOutcome.InsufficientBalance"/> if total balance is less than amount,
    /// or <see cref="DebitOutcome.DuplicateIdempotent"/> when the key was already used.</para>
    /// </summary>
    /// <param name="childId">Child whose account to debit (plain int; no cross-module FK).</param>
    /// <param name="amount">Credits to debit (must be &gt; 0).</param>
    /// <param name="reasonCode">Enum reason code as string (e.g. <c>"AiHint"</c>).</param>
    /// <param name="idempotencyKey">Caller-supplied per-request unique key.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<DebitResult> TryDebitAsync(
        int childId,
        int amount,
        string reasonCode,
        string idempotencyKey,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the current energy balance for <paramref name="childId"/>.
    /// Creates an empty account if none exists yet (idempotent bootstrap).
    /// </summary>
    Task<EnergyBalance> GetBalanceAsync(int childId, CancellationToken ct = default);
}
