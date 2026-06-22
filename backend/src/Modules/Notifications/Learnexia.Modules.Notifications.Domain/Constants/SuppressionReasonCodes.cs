namespace Learnexia.Modules.Notifications.Domain.Constants;

/// <summary>
/// P9-13. Stable, documented suppression-reason taxonomy for notification analytics.
/// Stored in <c>analytics.ActivityEvents.SuppressionReason</c> (varchar 32).
/// ALL values are ≤ 32 characters so they fit the column constraint.
///
/// <para><strong>Design note:</strong> the three arbiter reasons
/// (<c>GlobalBudgetExhausted</c>, <c>Cooldown</c>, <c>PriorityLost</c>) come from
/// <c>ReengagementEvaluator.NotEligibleReason.ToString()</c> and are already persisted
/// as strings in the v1 schema. They are mirrored here as constants so the FE/admin
/// legend can label them without referencing Notifications.Domain. Adding a constant here
/// does NOT change the string value emitted by the arbiter path.</para>
///
/// <para>All constants are non-user-facing technical identifiers (analytics codes) —
/// per CONVENTIONS §10, localization is not required.</para>
/// </summary>
public static class SuppressionReasonCodes
{
    // ── P9-13 new reasons ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Push channel was not attempted because the parent preference (or category default) has
    /// push disabled for this child/category. The in-app inbox row is still written; only the
    /// push channel is suppressed. No double-count with a Dispatched event.
    /// </summary>
    public const string PushPrefOff = "PushPrefOff";

    /// <summary>
    /// Push channel was not attempted because the recipient has zero active device tokens
    /// registered. The arbiter granted the push (if ShouldPush=true), but there is nothing
    /// to send to. The in-app inbox row is still written.
    /// </summary>
    public const string NoDeviceTokens = "NoDeviceTokens";

    /// <summary>
    /// The nudge was dropped by the dedupe guard before dispatch — a duplicate integration event
    /// for the same (student, category, day) or (student, tier-key) combination was already
    /// processed. No dispatch, no inbox row, no Dispatched event.
    /// </summary>
    public const string Deduped = "Deduped";

    // ── Existing arbiter reasons (mirrored from ReengagementEvaluator.NotEligibleReason) ──────
    // These strings are what .ToString() produces on the enum members. Mirrored here as constants
    // so the admin legend can reference a single file instead of the domain enum.

    /// <summary>
    /// The child's global per-day push budget (across ALL categories) has been reached.
    /// Emitted by <c>NudgeDispatcher.TryArbiterGrantAsync</c> on the P9-07 arbiter path.
    /// </summary>
    public const string GlobalBudgetExhausted = "GlobalBudgetExhausted";

    /// <summary>
    /// A per-type cooldown (Redis SETNX) prevented the same notification code from firing again
    /// within its configured TTL.
    /// Emitted by <c>NudgeDispatcher.TryArbiterGrantAsync</c> on the P9-07 arbiter path.
    /// </summary>
    public const string Cooldown = "Cooldown";

    /// <summary>
    /// A higher-priority category consumed the last available push slot in the daily budget
    /// before this nudge was dispatched.
    /// Emitted by <c>NudgeDispatcher.TryArbiterGrantAsync</c> on the P9-07 arbiter path.
    /// </summary>
    public const string PriorityLost = "PriorityLost";
}
