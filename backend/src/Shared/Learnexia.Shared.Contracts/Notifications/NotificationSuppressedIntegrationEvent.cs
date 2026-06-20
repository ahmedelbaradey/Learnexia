namespace Learnexia.Shared.Contracts.Notifications;

/// <summary>
/// Published by the <c>Notifications</c> module (<c>NudgeDispatcher.TryArbiterGrantAsync</c>)
/// when the P9-07 push arbiter denies a push channel send. Consumed by the <c>Analytics</c>
/// module to append a <c>NotificationSuppressed</c> <c>ActivityEvent</c> row.
///
/// <para><strong>No PII.</strong> Carries only opaque IDs + code/category/reason scalars:
/// <see cref="StudentId"/> is the identity int id (opaque); <see cref="Code"/> is a template key;
/// <see cref="Category"/> is the <c>NotificationCategory</c> enum int value (0–9);
/// <see cref="Reason"/> is the <c>ReengagementEvaluator.NotEligibleReason</c> enum name
/// (e.g. <c>GlobalBudgetExhausted</c>, <c>PriorityLost</c>, <c>Cooldown</c>).</para>
///
/// <para><strong>Semantics (v1): push-suppressed only.</strong> A Suppressed event is published
/// when the push channel is denied by the P9-07 arbiter; the in-app inbox row is STILL written.
/// So a single nudge can emit both a Suppressed (push denied) and a Dispatched (in-app delivered)
/// signal. Suppressed counts in admin dashboards represent <em>push-rationing</em>, not nudges
/// that were completely dropped. Pre-dispatch suppressions (parent-disabled, quiet-hours, daily
/// cap via <c>ReengagementEvaluator.Evaluate</c>) are a deferred follow-up — they are currently
/// captured only as log entries in the 11 re-engagement handlers.</para>
///
/// <para><strong>Module isolation:</strong> <see cref="Reason"/> is the enum member name as a
/// plain string so neither module references the other's domain. The Analytics sink stores the
/// string as-is; the admin layer buckets by string value.</para>
/// </summary>
/// <param name="EventId">Unique event identifier (UUID). Used as <c>SourceEventId</c> for idempotent ingest.</param>
/// <param name="OccurredOnUtc">UTC instant the suppression decision was made.</param>
/// <param name="StudentId">Opaque identity int id of the student recipient. No PII.</param>
/// <param name="Code">Stable notification template code (e.g. <c>STREAK_BROKEN</c>). Max 64 chars.</param>
/// <param name="Category">The <c>NotificationCategory</c> enum integer value (0–9).</param>
/// <param name="Reason">
/// The <c>ReengagementEvaluator.NotEligibleReason</c> enum member name, e.g.
/// <c>GlobalBudgetExhausted</c>, <c>PriorityLost</c>, <c>Cooldown</c>. Max 32 chars.
/// </param>
public sealed record NotificationSuppressedIntegrationEvent(
    Guid     EventId,
    DateTime OccurredOnUtc,
    int      StudentId,
    string   Code,
    int      Category,
    string   Reason) : IIntegrationEvent;
