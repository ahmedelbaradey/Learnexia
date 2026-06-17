namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Result returned by <see cref="ICreditSpendService.TryDebitAsync"/>.
/// </summary>
/// <param name="Charged">True when credits were successfully debited.</param>
/// <param name="FromGranted">Amount drawn from the granted pool (legacy CreditAccount model).</param>
/// <param name="FromPurchased">Amount drawn from the purchased pool (legacy CreditAccount model).</param>
/// <param name="ResultingTotal">Total balance after the debit.</param>
/// <param name="Outcome">Structured outcome code.</param>
public sealed record DebitResult(
    bool Charged,
    int FromGranted,
    int FromPurchased,
    int ResultingTotal,
    DebitOutcome Outcome)
{
    // ── P10-13 family-wallet extensions (init-only — NOT positional ctor params) ──
    // These are additive; the existing 5-arg positional ctor is unchanged so the 4 Ai
    // handlers and their unit tests continue to compile without any modification.

    /// <summary>
    /// Amount debited from the child's own <c>ChildEnergyAllocation</c> row (allocation-first path).
    /// Zero when the charge came entirely from the purchased fallback or was a duplicate/insufficient.
    /// </summary>
    public int FromAllocation { get; init; }

    /// <summary>
    /// Amount drawn from the shared family <c>PurchasedBalance</c> as a fallback when the child's
    /// allocation row was insufficient. Zero on the normal allocation-covered path.
    /// </summary>
    public int FromPurchasedFallback { get; init; }
}

/// <summary>Machine-readable debit outcome.</summary>
public enum DebitOutcome
{
    /// <summary>Debit applied successfully.</summary>
    Charged = 1,

    /// <summary>Insufficient balance — no debit written.</summary>
    InsufficientBalance = 2,

    /// <summary>Duplicate idempotency key — prior debit returned; no new row written.</summary>
    DuplicateIdempotent = 3,

    /// <summary>
    /// P10-15-BE-5: the child's seat is <c>NoSeatLocked</c> — spend denied before any balance is touched.
    /// The AI handler should surface <see cref="Resources.SharedResourcesKey.ChildSeatLockedNoEnergy"/>
    /// to the parent as a 403-level response.
    /// </summary>
    SeatLocked = 4,
}
