using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IAdminRefundQueryService"/> (P10-17-BE-6).
///
/// <para>Admin-privileged queries: not family-scoped. Looks up any payment's owning parent
/// and resolves the corresponding <c>FamilyEnergyAccount.Id</c>.</para>
/// </summary>
public sealed class AdminRefundQueryService : IAdminRefundQueryService
{
    private readonly BillingDbContext _db;

    public AdminRefundQueryService(BillingDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<int?> ResolveFamilyAccountIdByPaymentAsync(
        int purchasePaymentId,
        CancellationToken ct)
    {
        // Resolve parent from the payment (admin is NOT family-scoped — can refund any family).
        var parentUserId = await _db.Payments
            .AsNoTracking()
            .Where(p => p.Id == purchasePaymentId && p.Kind == PaymentKind.Pack)
            .Select(p => (int?)p.ParentUserId)
            .FirstOrDefaultAsync(ct);

        if (parentUserId is null)
            return null;

        return await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .Where(w => w.ParentUserId == parentUserId.Value)
            .Select(w => (int?)w.Id)
            .FirstOrDefaultAsync(ct);
    }
}
