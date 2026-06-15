namespace Learnexia.Modules.Billing.Application.Features.Credits.Dtos;

/// <summary>
/// Read model returned by <c>GetCreditAccountQuery</c>. Surfaces only balance fields —
/// raw ledger rows are not exposed to the child app.
/// </summary>
public record CreditAccountDto
{
    public int ChildId { get; init; }
    public int GrantedBalance { get; init; }
    public int PurchasedBalance { get; init; }
    public int TotalBalance { get; init; }
    public DateTime? GrantExpiresAtUtc { get; init; }

    /// <summary>Daily credits spent so far (lazy-reset at local midnight).</summary>
    public int DailyUsed { get; init; }
}
