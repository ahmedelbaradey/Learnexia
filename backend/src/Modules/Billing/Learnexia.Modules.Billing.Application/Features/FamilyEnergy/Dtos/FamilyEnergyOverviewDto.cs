namespace Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Dtos;

/// <summary>
/// Read-model returned by <c>GetFamilyEnergyOverviewQuery</c> (P10-13-BE-10).
/// Consumed by <c>GET /api/Billing/FamilyEnergy/Overview</c> — parent-JWT-only endpoint.
/// </summary>
public sealed class FamilyEnergyOverviewDto
{
    /// <summary>The <c>FamilyEnergyAccount.Id</c> for this family. Used internally for service calls (P10-17).</summary>
    public int FamilyAccountId { get; init; }

    /// <summary>Temporary subscription balance (monthly, resets each cycle).</summary>
    public int SubscriptionBalance { get; init; }

    /// <summary>Permanent purchased balance (pack-purchase reserve, never expires, shared family).</summary>
    public int PurchasedBalance { get; init; }

    /// <summary>When the current subscription allocation cycle expires; null when inactive.</summary>
    public DateTime? SubscriptionExpiresAtUtc { get; init; }

    /// <summary>Per-child allocation snapshots for the current cycle.</summary>
    public IReadOnlyList<ChildAllocationDto> Children { get; init; } = Array.Empty<ChildAllocationDto>();
}

/// <summary>Per-child allocation snapshot within the family energy overview.</summary>
public sealed class ChildAllocationDto
{
    /// <summary>Child id (loose int — no cross-module FK).</summary>
    public int ChildId { get; init; }

    /// <summary>Credits allocated to this child for the current cycle.</summary>
    public int Allocated { get; init; }

    /// <summary>Credits spent by this child in the current cycle.</summary>
    public int Spent { get; init; }

    /// <summary>Unspent remaining (Allocated − Spent, always ≥ 0).</summary>
    public int Remaining { get; init; }
}
