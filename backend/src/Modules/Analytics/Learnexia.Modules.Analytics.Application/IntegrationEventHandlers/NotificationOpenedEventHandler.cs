using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.Notifications;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// P9-11. Cross-module consumer of <see cref="NotificationOpenedIntegrationEvent"/>
/// (produced by the Notifications module's <c>MarkNotificationReadCommandHandler</c>
/// when a student marks a single inbox notification as read).
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>NotificationOpened</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Design constraints (P9-11):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — the store skips if a row with the same
///   <see cref="NotificationOpenedIntegrationEvent.EventId"/> already exists.</item>
///   <item><b>Fail-soft</b> — the store swallows persistence exceptions; this handler never
///   throws to the publisher. A sink failure MUST NOT affect the mark-read response.</item>
///   <item><b>PII-light</b> — only opaque int IDs, template code, category int, and UTC
///   timestamp are written; no PII.</item>
///   <item><b>Module isolation</b> — subscribes via <c>Shared.Contracts</c> only.
///   Category is stored as a plain int.</item>
///   <item><b>v1 scope:</b> inbox-read path only. Push-tap Opened is deferred to P9-02 FE.
///   Mark-all-read Opened is deferred (the bulk path uses ExecuteUpdateAsync without materialising
///   entities — an extra query would be needed). Open-rate therefore undercounts opens until
///   P9-02 and mark-all-read emission ship.</item>
/// </list>
/// </summary>
public sealed class NotificationOpenedEventHandler
    : INotificationHandler<NotificationOpenedIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public NotificationOpenedEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(
        NotificationOpenedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received NotificationOpenedIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"Code={notification.Code}, Category={notification.Category}).");

        var activityEvent = new ActivityEvent
        {
            StudentId             = notification.StudentId,
            EventType             = "NotificationOpened",
            OccurredAtUtc         = notification.OccurredOnUtc,
            SourceEventId         = notification.EventId,
            NotificationCode      = notification.Code,
            NotificationCategory  = notification.Category,
            NotificationChannels  = null,
            SuppressionReason     = null,
            SubjectCode           = null,
            DurationSeconds       = null,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
