namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for the admin refund read path (P10-17-BE-6).
///
/// <para>Provides admin-privileged queries that are NOT family-scoped. An admin/support agent
/// can look up any family's purchased payment without the IDOR family-scope restriction that
/// applies to the parent path.</para>
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF query logic lives in the Infrastructure implementation.</para>
/// </summary>
public interface IAdminRefundQueryService
{
    /// <summary>
    /// Resolves the <c>FamilyEnergyAccount.Id</c> for the parent who owns the given pack payment.
    ///
    /// <para>Used by the admin refund flow to locate the target family without requiring
    /// the admin's JWT to be family-scoped (the admin can refund any family's pack).</para>
    /// </summary>
    /// <param name="purchasePaymentId">The <c>Payment.Id</c> of the pack purchase.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>FamilyEnergyAccount.Id</c> if found; <c>null</c> if the payment or wallet does not exist.</returns>
    Task<int?> ResolveFamilyAccountIdByPaymentAsync(int purchasePaymentId, CancellationToken ct);
}
