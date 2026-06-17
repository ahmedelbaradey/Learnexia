using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// Per-child daily soft-cap counter that SURVIVES allocation reset/expiry (OQ-G, P10-13-BE-4).
///
/// <para>One row per child. Tracks the running count for the child's local calendar day.
/// Lazy-reset: when <see cref="DailyUsedDateLocal"/> is stale relative to today in the
/// child's timezone, <see cref="DailyUsed"/> is treated as zero (and reset to 0 on next write).
/// The write happens atomically inside the spend transaction (same path as before — no
/// separate transaction needed).</para>
///
/// <para>Module isolation: <see cref="ChildId"/> is a plain int — no cross-module FK.</para>
/// </summary>
public class ChildDailyUsage : FullAuditedEntity
{
    /// <summary>Child whose daily usage this row tracks (loose int, no cross-module FK).</summary>
    public int ChildId { get; set; }

    /// <summary>Credits spent on the current local-day.</summary>
    public int DailyUsed { get; set; }

    /// <summary>
    /// The calendar date (child's local timezone) for which <see cref="DailyUsed"/> was last
    /// incremented, stored as <c>yyyy-MM-dd</c>. Null on first use.
    /// </summary>
    public string? DailyUsedDateLocal { get; set; }

    /// <summary>IANA timezone id for the child (e.g. <c>Africa/Cairo</c>).</summary>
    public string ChildTimeZoneId { get; set; } = "Africa/Cairo";

    /// <summary>Creates a new usage row for a child.</summary>
    public static ChildDailyUsage Create(int childId, string childTimeZoneId = "Africa/Cairo")
        => new()
        {
            ChildId           = childId,
            DailyUsed         = 0,
            ChildTimeZoneId   = childTimeZoneId,
        };
}
