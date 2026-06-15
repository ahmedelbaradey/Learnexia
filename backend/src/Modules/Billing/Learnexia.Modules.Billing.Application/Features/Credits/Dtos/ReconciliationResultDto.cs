namespace Learnexia.Modules.Billing.Application.Features.Credits.Dtos;

/// <summary>
/// Result returned by <c>ReconcileAccountQuery</c>. Compares the stored account
/// balances against the sum of all ledger rows. Does NOT auto-heal.
/// </summary>
public record ReconciliationResultDto
{
    public int ChildId { get; init; }
    public int StoredGrantedBalance { get; init; }
    public int StoredPurchasedBalance { get; init; }
    public int LedgerGrantedBalance { get; init; }
    public int LedgerPurchasedBalance { get; init; }
    public bool HasDrift { get; init; }
    public int GrantedDrift => LedgerGrantedBalance - StoredGrantedBalance;
    public int PurchasedDrift => LedgerPurchasedBalance - StoredPurchasedBalance;
}
