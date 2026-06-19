using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="MissionCompletedIntegrationEvent"/> (re-published by the
/// Gamification module from <c>MissionCompletedDomainEvent</c>).
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>MissionCompleted</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Design constraints (P5-03 brief §Capture consumers):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — dedup on <see cref="MissionCompletedIntegrationEvent.EventId"/>
///   via the store's AnyAsync fast-path + unique-index guard.</item>
///   <item><b>Fail-soft</b> — the store swallows all persistence exceptions; never propagates.</item>
///   <item><b>PII-light</b> — only plain int IDs, event type, and UTC timestamp are written.
///   <c>MissionCode</c> and <c>RewardXp</c> are NOT stored (not required for DAU/session/retention KPIs).</item>
///   <item><b>Module isolation</b> — no project reference to <c>Gamification.*</c>.</item>
///   <item><b>SubjectCode / DurationSeconds</b> — null; the payload carries neither.</item>
/// </list>
/// </summary>
public sealed class MissionCompletedEventHandler : INotificationHandler<MissionCompletedIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public MissionCompletedEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(MissionCompletedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received MissionCompletedIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"MissionCode={notification.MissionCode}).");

        var activityEvent = new ActivityEvent
        {
            StudentId       = notification.StudentId,
            EventType       = "MissionCompleted",
            SubjectCode     = null,   // not carried on this event payload
            DurationSeconds = null,   // not carried on this event payload
            OccurredAtUtc   = notification.OccurredOnUtc,
            SourceEventId   = notification.EventId,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
