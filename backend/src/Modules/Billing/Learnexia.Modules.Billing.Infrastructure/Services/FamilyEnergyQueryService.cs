using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Dtos;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="IFamilyEnergyQueryService"/> (P10-13-BE-10).
/// Owns all EF query / projection logic; the Application handler stays EF-free (Option C).
/// </summary>
public sealed class FamilyEnergyQueryService : IFamilyEnergyQueryService
{
    private readonly BillingDbContext _db;

    public FamilyEnergyQueryService(BillingDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<FamilyEnergyOverviewDto?> GetOverviewAsync(int parentUserId, CancellationToken ct = default)
    {
        var wallet = await _db.FamilyEnergyAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.ParentUserId == parentUserId, ct);

        if (wallet is null)
            return null;

        // Load the most-recent active allocation row per child (max CycleStartUtc).
        var allAllocations = await _db.ChildEnergyAllocations
            .AsNoTracking()
            .Where(a => a.FamilyEnergyAccountId == wallet.Id)
            .OrderByDescending(a => a.CycleStartUtc)
            .ToListAsync(ct);

        // Distinct by ChildId — take the latest cycle row per child.
        var latestPerChild = allAllocations
            .GroupBy(a => a.ChildId)
            .Select(g => g.First())
            .ToList();

        var children = latestPerChild
            .Select(a => new ChildAllocationDto
            {
                ChildId   = a.ChildId,
                Allocated = a.AllocatedAmount,
                Spent     = a.SpentAmount,
                Remaining = a.Remaining,
            })
            .ToList();

        return new FamilyEnergyOverviewDto
        {
            SubscriptionBalance     = wallet.SubscriptionBalance,
            PurchasedBalance        = wallet.PurchasedBalance,
            SubscriptionExpiresAtUtc = wallet.SubscriptionExpiresAtUtc,
            Children                = children,
        };
    }
}
