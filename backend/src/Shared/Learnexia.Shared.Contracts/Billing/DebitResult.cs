namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Result returned by <see cref="ICreditSpendService.TryDebitAsync"/>.
/// </summary>
/// <param name="Charged">True when credits were successfully debited.</param>
/// <param name="FromGranted">Amount drawn from the granted pool.</param>
/// <param name="FromPurchased">Amount drawn from the purchased pool.</param>
/// <param name="ResultingTotal">Total balance after the debit.</param>
/// <param name="Outcome">Structured outcome code.</param>
public sealed record DebitResult(
    bool Charged,
    int FromGranted,
    int FromPurchased,
    int ResultingTotal,
    DebitOutcome Outcome);

/// <summary>Machine-readable debit outcome.</summary>
public enum DebitOutcome
{
    /// <summary>Debit applied successfully.</summary>
    Charged = 1,

    /// <summary>Insufficient balance — no debit written.</summary>
    InsufficientBalance = 2,

    /// <summary>Duplicate idempotency key — prior debit returned; no new row written.</summary>
    DuplicateIdempotent = 3,
}
