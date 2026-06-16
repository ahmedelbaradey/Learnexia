namespace Learnexia.Modules.Billing.Application.Features.Credits.Dtos;

/// <summary>
/// Result returned by <c>ReconcileAccountQuery</c>. Compares the stored family wallet
/// balances (<c>FamilyEnergyAccount.SubscriptionBalance</c> / <c>PurchasedBalance</c>)
/// against the sum of all ledger rows for the family.
/// Does NOT involve <c>CreditAccount</c> (retired).
/// Does NOT auto-heal.
/// </summary>
public record ReconciliationResultDto
{
    /// <summary>Parent user ID whose family wallet was reconciled.</summary>
    public int ParentId { get; init; }

    // ── Stored wallet balances (snapshot at query time) ───────────────────────
    public int StoredSubscriptionBalance { get; init; }
    public int StoredPurchasedBalance { get; init; }

    // ── Ledger-computed balances (recomputed from all CreditTransaction rows) ─
    public int LedgerSubscriptionBalance { get; init; }
    public int LedgerPurchasedBalance { get; init; }

    // ── Drift indicators ─────────────────────────────────────────────────────
    public bool HasDrift { get; init; }
    public int SubscriptionDrift => LedgerSubscriptionBalance - StoredSubscriptionBalance;
    public int PurchasedDrift => LedgerPurchasedBalance - StoredPurchasedBalance;
}
