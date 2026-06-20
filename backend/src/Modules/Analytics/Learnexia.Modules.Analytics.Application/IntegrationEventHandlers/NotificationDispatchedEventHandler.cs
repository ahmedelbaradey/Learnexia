using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.Notifications;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// P9-11. Cross-module consumer of <see cref="NotificationDispatchedIntegrationEvent"/>
/// (produced by the Notifications module's <c>NudgeDispatcher</c> after a successful dispatch).
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>NotificationDispatched</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Design constraints (P9-11):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — the store skips if a row with the same
///   <see cref="NotificationDispatchedIntegrationEvent.EventId"/> already exists
///   (fast-path AnyAsync check + unique index guard).</item>
///   <item><b>Fail-soft</b> — the store swallows persistence exceptions; this handler never
///   throws to the publisher. A sink failure MUST NOT affect the dispatch path.</item>
///   <item><b>PII-light</b> — only opaque int IDs, template code, category int, channels bitmask,
///   and UTC timestamp are written; no names, emails, or body text.</item>
///   <item><b>Module isolation</b> — subscribes via <c>Shared.Contracts</c> only;
///   no project reference to <c>Notifications.*</c>. Category is stored as a plain int.</item>
/// </list>
/// </summary>
public sealed class NotificationDispatchedEventHandler
    : INotificationHandler<NotificationDispatchedIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public NotificationDispatchedEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(
        NotificationDispatchedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received NotificationDispatchedIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"Code={notification.Code}, Category={notification.Category}, " +
            $"DeliveredChannels={notification.DeliveredChannels}).");

        var activityEvent = new ActivityEvent
        {
            StudentId             = notification.StudentId,
            EventType             = "NotificationDispatched",
            OccurredAtUtc         = notification.OccurredOnUtc,
            SourceEventId         = notification.EventId,
            NotificationCode      = notification.Code,
            NotificationCategory  = notification.Category,
            NotificationChannels  = notification.DeliveredChannels,
            SuppressionReason     = null,
            SubjectCode           = null,
            DurationSeconds       = null,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
