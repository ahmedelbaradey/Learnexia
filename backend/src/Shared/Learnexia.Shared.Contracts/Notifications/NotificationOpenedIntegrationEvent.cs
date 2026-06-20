namespace Learnexia.Shared.Contracts.Notifications;

/// <summary>
/// Published by the <c>Notifications</c> module (<c>MarkNotificationReadCommandHandler</c>)
/// after a student marks a single inbox notification as read. Consumed by the <c>Analytics</c>
/// module to append a <c>NotificationOpened</c> <c>ActivityEvent</c> row.
///
/// <para><strong>No PII.</strong> Carries only opaque IDs + code/category scalars:
/// <see cref="StudentId"/> is the identity int id (opaque); <see cref="Code"/> is a template key
/// (e.g. <c>BADGE_EARNED</c>); <see cref="Category"/> is the <c>NotificationCategory</c> enum
/// int value (0–9).</para>
///
/// <para><strong>Semantics (v1): inbox-read path only.</strong> This event is emitted only when
/// the student explicitly marks a notification as read via the inbox. Push-tap opens are
/// <strong>DEFERRED to P9-02 FE</strong> (the deep-link handler). The admin open-rate therefore
/// undercounts opens until P9-02 ships — the endpoint documentation states this explicitly.
/// Mark-all-read bulk path is also deferred for v1: that path uses <c>ExecuteUpdateAsync</c>
/// without materialising entities, so emitting one Opened per row would require an extra
/// query; per the brief's guidance, mark-all-read Opened emission is deferred.</para>
///
/// <para><strong>Module isolation:</strong> <see cref="Category"/> is a plain <c>int</c> (the
/// <c>NotificationCategory</c> enum value) so neither module references the other's domain.</para>
/// </summary>
/// <param name="EventId">Unique event identifier (UUID). Used as <c>SourceEventId</c> for idempotent ingest.</param>
/// <param name="OccurredOnUtc">UTC instant the notification was marked as read.</param>
/// <param name="StudentId">Opaque identity int id of the student who opened the notification. No PII.</param>
/// <param name="Code">Stable notification template code (e.g. <c>BADGE_EARNED</c>). Max 64 chars.</param>
/// <param name="Category">The <c>NotificationCategory</c> enum integer value (0–9).</param>
public sealed record NotificationOpenedIntegrationEvent(
    Guid     EventId,
    DateTime OccurredOnUtc,
    int      StudentId,
    string   Code,
    int      Category) : IIntegrationEvent;
