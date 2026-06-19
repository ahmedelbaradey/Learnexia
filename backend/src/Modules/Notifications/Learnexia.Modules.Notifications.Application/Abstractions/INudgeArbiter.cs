using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Domain.Services;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Cross-category push-budget arbitration gate for P9-07.
///
/// <para>The arbiter decides whether the PUSH channel is permitted for a given nudge. It does NOT
/// affect the in-app inbox row — <see cref="INudgeDispatcher"/> ALWAYS writes that row regardless
/// of the arbiter's decision (the inbox is the durable receipt). Only the push bit is rationed.</para>
///
/// <para>Architecture note (no-batching / emergent priority):
/// Because integration-event handlers fire independently per event — there is no scheduler or
/// batch window — cross-category priority is realised EMERGENTLY. Higher-priority types (e.g.
/// StreakAtRisk) consume the scarce daily push budget first; lower-priority types encounter
/// <see cref="ReengagementEvaluator.NotEligibleReason.GlobalBudgetExhausted"/> later in the day.
/// The arbiter does NOT collect and compare pending candidates in one pass — it makes a per-call
/// decision based on the current budget snapshot and cooldown state. This is consistent with the
/// existing event-driven design and is documented here to prevent a reviewer from expecting a
/// windowed scheduler. See HANDOFF.md for more context.</para>
///
/// <para>Budget authority: push count is derived from durable DB inbox rows
/// (<c>INotificationInboxService.CountPushesSentTodayAsync</c>) so the budget is enforced even
/// during a Redis outage. Redis is used only for the per-type cooldown (fail-open if unavailable).</para>
///
/// Registered as Scoped in <c>AddNotificationsInfrastructure</c>.
/// </summary>
public interface INudgeArbiter
{
    /// <summary>
    /// Evaluates whether the push channel should be permitted for a nudge with the given
    /// <paramref name="category"/> and <paramref name="typeCode"/>, and — if granted —
    /// atomically records the cooldown so this type cannot fire again within its TTL.
    ///
    /// <para>The in-app inbox row decision is NOT governed by this method; callers must always
    /// set <c>NudgeMessage.ShouldInApp</c> from the parent's prefs and pass
    /// <c>NudgeMessage.ShouldPush = arbiterResult.ShouldPush</c>.</para>
    /// </summary>
    /// <param name="childId">The student (child) whose budget is checked.</param>
    /// <param name="category">Notification category (used for priority-order lookup).</param>
    /// <param name="typeCode">Stable notification code, e.g. "LEAGUE_TIER_CHANGED" (used for cooldown key).</param>
    /// <param name="globalBudget">
    ///   The per-child global daily push budget. Sourced from
    ///   <c>ChildReengagementPreference.GlobalDailyPushBudget ?? IGlobalSettingsProvider.GetInt("Notifications:GlobalDailyPushBudget", 4)</c>.
    /// </param>
    /// <param name="nowUtc">Current UTC instant (from <c>ISystemClock</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///   <see cref="ReengagementEvaluator.ArbiterResult"/> — <c>ShouldPush=true</c> when the push is
    ///   granted; <c>ShouldPush=false</c> with a suppress reason otherwise.
    /// </returns>
    Task<ReengagementEvaluator.ArbiterResult> ArbitrateAsync(
        int childId,
        NotificationCategory category,
        string typeCode,
        int globalBudget,
        DateTime nowUtc,
        CancellationToken ct = default);
}
