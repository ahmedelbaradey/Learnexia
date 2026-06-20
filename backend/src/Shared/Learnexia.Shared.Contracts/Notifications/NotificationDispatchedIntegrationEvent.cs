namespace Learnexia.Shared.Contracts.Notifications;

/// <summary>
/// Published by the <c>Notifications</c> module (<c>NudgeDispatcher</c>) after a nudge's
/// in-app inbox row has been persisted and the final <see cref="DeliveredChannels"/> bitmask
/// is known. Consumed by the <c>Analytics</c> module to append a
/// <c>NotificationDispatched</c> <c>ActivityEvent</c> row.
///
/// <para><strong>No PII.</strong> Carries only opaque IDs + code/category/channels scalars:
/// <see cref="StudentId"/> is the identity int id (opaque); <see cref="Code"/> is a template key
/// (e.g. <c>BADGE_EARNED</c>); <see cref="Category"/> is the <c>NotificationCategory</c> enum
/// int value (0–9); <see cref="DeliveredChannels"/> is the delivery bitmask
/// (inApp = 4, push = 2).</para>
///
/// <para><strong>Semantics:</strong> one Dispatched event is published per nudge dispatch —
/// even when push is suppressed (the in-app receipt always lands). The
/// <see cref="DeliveredChannels"/> bitmask records which channels actually delivered:
/// 4 = inApp only; 6 = inApp + push. Open-rate = Opened / Dispatched.</para>
///
/// <para><strong>Module isolation:</strong> <see cref="Category"/> is a plain <c>int</c> (the
/// <c>NotificationCategory</c> enum value) so neither module references the other's domain.
/// The Analytics sink stores the int as-is; the admin layer maps int → label.</para>
/// </summary>
/// <param name="EventId">Unique event identifier (UUID). Used as <c>SourceEventId</c> for idempotent ingest.</param>
/// <param name="OccurredOnUtc">UTC instant the dispatch completed (after SaveChangesAsync succeeded).</param>
/// <param name="StudentId">Opaque identity int id of the student recipient. No PII.</param>
/// <param name="Code">Stable notification template code (e.g. <c>STREAK_BROKEN</c>). Max 64 chars.</param>
/// <param name="Category">The <c>NotificationCategory</c> enum integer value (0–9).</param>
/// <param name="DeliveredChannels">
/// Bitmask of channels on which the notification was actually delivered:
/// 4 = inApp, 2 = push, 6 = inApp + push.
/// </param>
public sealed record NotificationDispatchedIntegrationEvent(
    Guid     EventId,
    DateTime OccurredOnUtc,
    int      StudentId,
    string   Code,
    int      Category,
    int      DeliveredChannels) : IIntegrationEvent;
