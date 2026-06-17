using Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Dtos;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Service seam (Option C) for the parent family-energy read path (P10-13-BE-10).
///
/// <para><strong>Option C contract:</strong> Application-layer handlers inject THIS interface.
/// All EF query / projection logic lives in the Infrastructure implementation.
/// The handler stays EF-free.</para>
/// </summary>
public interface IFamilyEnergyQueryService
{
    /// <summary>
    /// Returns the family energy overview for a parent: wallet balances,
    /// subscription expiry, and per-child allocation snapshots.
    ///
    /// <para>Family-scope IDOR: the Infrastructure impl must ensure the account
    /// belongs to <paramref name="parentUserId"/>. Handler additionally checks
    /// the authenticated user id from JWT.</para>
    /// </summary>
    Task<FamilyEnergyOverviewDto?> GetOverviewAsync(int parentUserId, CancellationToken ct = default);
}
