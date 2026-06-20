using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.Notifications;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// P9-11. Cross-module consumer of <see cref="NotificationSuppressedIntegrationEvent"/>
/// (produced by the Notifications module's <c>NudgeDispatcher.TryArbiterGrantAsync</c>
/// when the P9-07 push arbiter denies the push channel).
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>NotificationSuppressed</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Design constraints (P9-11):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — the store skips if a row with the same
///   <see cref="NotificationSuppressedIntegrationEvent.EventId"/> already exists.</item>
///   <item><b>Fail-soft</b> — the store swallows persistence exceptions; this handler never
///   throws to the publisher.</item>
///   <item><b>PII-light</b> — only opaque int IDs, template code, category int, suppression
///   reason string, and UTC timestamp are written; no PII.</item>
///   <item><b>Module isolation</b> — subscribes via <c>Shared.Contracts</c> only.
///   Category is stored as a plain int; Reason is the enum member name as a string
///   (e.g. <c>GlobalBudgetExhausted</c>).</item>
///   <item><b>v1 scope:</b> only P9-07 push-rationing suppressions are captured (arbiter
///   reasons: GlobalBudgetExhausted, PriorityLost, Cooldown). Pre-dispatch suppressions
///   (parent-disabled, quiet-hours, per-category daily-cap) are a deferred follow-up.</item>
/// </list>
/// </summary>
public sealed class NotificationSuppressedEventHandler
    : INotificationHandler<NotificationSuppressedIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public NotificationSuppressedEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(
        NotificationSuppressedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received NotificationSuppressedIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"Code={notification.Code}, Category={notification.Category}, " +
            $"Reason={notification.Reason}).");

        var activityEvent = new ActivityEvent
        {
            StudentId             = notification.StudentId,
            EventType             = "NotificationSuppressed",
            OccurredAtUtc         = notification.OccurredOnUtc,
            SourceEventId         = notification.EventId,
            NotificationCode      = notification.Code,
            NotificationCategory  = notification.Category,
            NotificationChannels  = null,
            SuppressionReason     = notification.Reason,
            SubjectCode           = null,
            DurationSeconds       = null,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
