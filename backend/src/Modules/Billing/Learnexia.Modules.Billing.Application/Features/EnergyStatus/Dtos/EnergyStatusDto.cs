using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Features.EnergyStatus.Dtos;

/// <summary>
/// Read-model returned by <c>EnergyStatusQuery</c>. Consumed by the P10-10 kid energy UI
/// (via <c>GET /api/Billing/Energy/{childId}/Status</c>) and by the P10-03 pre-authorize check.
///
/// <para>This is the ONLY backend obligation toward the kid energy meter (P10-10 [FE]).</para>
/// </summary>
public sealed class EnergyStatusDto
{
    // ── Monthly balance ────────────────────────────────────────────────────────────

    /// <summary>Current granted-pool balance.</summary>
    public int GrantedBalance { get; init; }

    /// <summary>Current purchased-pool balance (never expires).</summary>
    public int PurchasedBalance { get; init; }

    /// <summary>Combined total (Granted + Purchased).</summary>
    public int TotalBalance { get; init; }

    /// <summary>When the current monthly grant expires; null when no active grant.</summary>
    public DateTime? GrantExpiresAtUtc { get; init; }

    // ── Daily usage ────────────────────────────────────────────────────────────────

    /// <summary>Credits spent today (child local-day, lazy-reset).</summary>
    public int DailyUsed { get; init; }

    /// <summary>Daily soft-cap threshold from <c>GlobalSettings</c>.</summary>
    public int DailyCap { get; init; }

    /// <summary>True when <see cref="DailyUsed"/> &ge; <see cref="DailyCap"/>.</summary>
    public bool DailyCapReached { get; init; }

    // ── Low-energy flag ────────────────────────────────────────────────────────────

    /// <summary>True when <see cref="TotalBalance"/> is &le; 10 % of the monthly allotment.</summary>
    public bool LowEnergy { get; init; }

    // ── Warning state ──────────────────────────────────────────────────────────────

    /// <summary>Highest-severity applicable warning state. <c>None</c> means healthy.</summary>
    public WarningState WarningState { get; init; }
}
