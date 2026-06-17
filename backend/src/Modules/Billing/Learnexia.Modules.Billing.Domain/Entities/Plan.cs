using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// A subscription plan definition row (Free / Premium).
///
/// <para>Plans are seeded once; their <em>feature benefits</em> (monthly grant amount, daily cap)
/// are NOT stored here — they are always resolved at runtime from
/// <see cref="Constants.GlobalSettingKeys"/> via <c>IGlobalSettingsProvider</c> so an admin can
/// change the economy without a redeployment. The plan is purely an identity/label entity.</para>
///
/// <para>Prices (<c>199 EGP/month</c>, <c>1990 EGP/year</c>) are also stored in
/// <c>GlobalSettings</c> (<c>subscription.monthly_price_egp</c> / <c>subscription.annual_price_egp</c>)
/// and NOT on this entity.</para>
/// </summary>
public class Plan : FullAuditedEntity
{
    /// <summary>Stable tier identifier (Free = 0, Premium = 1). Unique.</summary>
    public PlanCode Code { get; set; }

    /// <summary>Display name (e.g. "Free", "Premium"). Max 100 chars.</summary>
    public string Name { get; set; } = null!;

    /// <summary>Whether this plan is currently offered to new subscribers.</summary>
    public bool IsActive { get; set; } = true;

    // ── P10-14: Seat entitlement ─────────────────────────────────────────────────

    /// <summary>
    /// Number of child seats included in this plan tier at no extra cost (P10-14-BE-1).
    /// Config-seeded per tier: Free = 1, Premium = 3 (OQ-C — locked 2026-06-16).
    /// Resolved from <c>GlobalSettings</c> via seed; never hard-coded in business logic.
    /// </summary>
    public int IncludedSeats { get; set; }
}
