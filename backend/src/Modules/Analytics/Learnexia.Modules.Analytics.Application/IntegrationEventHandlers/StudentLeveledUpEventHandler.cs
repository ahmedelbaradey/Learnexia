using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="StudentLeveledUpIntegrationEvent"/> (re-published by the
/// Gamification module; the event's doc names P5-03 analytics as the intended consumer).
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>StudentLeveledUp</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Design constraints (P5-03 brief §Capture consumers):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — dedup on <see cref="StudentLeveledUpIntegrationEvent.EventId"/>
///   via the store's AnyAsync fast-path + unique-index guard.</item>
///   <item><b>Fail-soft</b> — the store swallows all persistence exceptions; never propagates.</item>
///   <item><b>PII-light</b> — only plain int IDs, event type, and UTC timestamp are written.
///   <c>OldLevel</c> and <c>NewLevel</c> are NOT stored (no column for them; not needed for
///   DAU/session/retention KPIs).</item>
///   <item><b>Module isolation</b> — no project reference to <c>Gamification.*</c>.</item>
///   <item><b>SubjectCode / DurationSeconds</b> — null; the payload carries neither.</item>
/// </list>
/// </summary>
public sealed class StudentLeveledUpEventHandler : INotificationHandler<StudentLeveledUpIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public StudentLeveledUpEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(StudentLeveledUpIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received StudentLeveledUpIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"OldLevel={notification.OldLevel}, NewLevel={notification.NewLevel}).");

        var activityEvent = new ActivityEvent
        {
            StudentId       = notification.StudentId,
            EventType       = "StudentLeveledUp",
            SubjectCode     = null,   // not carried on this event payload
            DurationSeconds = null,   // not carried on this event payload
            OccurredAtUtc   = notification.OccurredOnUtc,
            SourceEventId   = notification.EventId,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
