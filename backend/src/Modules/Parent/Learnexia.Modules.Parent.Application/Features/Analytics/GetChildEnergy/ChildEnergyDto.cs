namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildEnergy;

/// <summary>
/// E7 response DTO — energy balance and weekly-usage breakdown for the parent's energy screen.
/// Covers all fields from <c>EnergyBalanceStub</c> + <c>EnergyUsageStub[]</c> in the FE gold contract.
/// </summary>
public sealed class ChildEnergyDto
{
    /// <summary>Credits remaining in the current allocation cycle (0 when no active cycle).</summary>
    public int Remaining { get; init; }

    /// <summary>Total credits allocated for the current cycle (0 when no active cycle).</summary>
    public int Allocated { get; init; }

    /// <summary>Credits spent so far this cycle (0 when no active cycle).</summary>
    public int Spent { get; init; }

    /// <summary>Shared family purchased-pack balance (0 when no wallet).</summary>
    public int PurchasedBalance { get; init; }

    /// <summary>When the current allocation cycle expires (null when no active cycle).</summary>
    public DateTime? CycleEndsAtUtc { get; init; }

    /// <summary>
    /// Per-helper-kind usage counts for the rolling-7-day window.
    /// Always contains exactly 4 entries (hints / explain / deep / practice).
    /// Zero counts when unused.
    /// </summary>
    public IReadOnlyList<EnergyUsageItemDto> WeeklyUsage { get; init; } = [];
}

/// <summary>Per-helper-kind energy usage item.</summary>
public sealed class EnergyUsageItemDto
{
    /// <summary>Helper kind identifier: one of hints, explain, deep, practice.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Number of spend transactions of this kind in the rolling-7-day window.</summary>
    public int Count { get; init; }
}
