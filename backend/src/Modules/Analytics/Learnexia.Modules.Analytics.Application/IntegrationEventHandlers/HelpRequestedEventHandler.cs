using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="HelpRequestedIntegrationEvent"/> (emitted fire-and-forget
/// at the entry of a helper handler, before any LLM call).
/// The event's doc explicitly names P5-03 product analytics as the intended consumer.
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>AiHelpRequested</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Design constraints (P5-03 brief §Capture consumers):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — dedup on <see cref="HelpRequestedIntegrationEvent.EventId"/>
///   via the store's AnyAsync fast-path + unique-index guard.</item>
///   <item><b>Fail-soft</b> — the store swallows all persistence exceptions; never propagates.</item>
///   <item><b>PII-light</b> — only plain int IDs, event type, and UTC timestamp are written.
///   <c>Intent</c>, <c>SkillId</c>, and <c>QuestionId</c> are NOT stored (engagement funnel
///   analysis uses EventType aggregates, not individual intent/skill breakdown in v1).</item>
///   <item><b>Module isolation</b> — no project reference to <c>Ai.*</c> or <c>AiTutor.*</c>
///   module projects; subscribes via <c>Shared.Contracts.AiTutor</c> only.</item>
///   <item><b>SubjectCode / DurationSeconds</b> — null; the payload carries <c>SkillId</c>
///   (not <c>SubjectCode</c>) and no duration.</item>
/// </list>
/// </summary>
public sealed class HelpRequestedEventHandler : INotificationHandler<HelpRequestedIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public HelpRequestedEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(HelpRequestedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received HelpRequestedIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"SkillId={notification.SkillId}).");

        var activityEvent = new ActivityEvent
        {
            StudentId       = notification.StudentId,
            EventType       = "AiHelpRequested",
            SubjectCode     = null,   // payload carries SkillId, not SubjectCode (OQ-3)
            DurationSeconds = null,   // not carried on this event payload
            OccurredAtUtc   = notification.OccurredOnUtc,
            SourceEventId   = notification.EventId,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
