using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="HelpDeliveredIntegrationEvent"/> (emitted fire-and-forget
/// when the AI helper response is successfully delivered to the student).
/// The event's doc explicitly names P5-03 product analytics as the intended consumer.
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>AiHelpDelivered</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Design constraints (P5-03 brief §Capture consumers):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — dedup on <see cref="HelpDeliveredIntegrationEvent.EventId"/>
///   via the store's AnyAsync fast-path + unique-index guard.</item>
///   <item><b>Fail-soft</b> — the store swallows all persistence exceptions; never propagates.</item>
///   <item><b>PII-light</b> — only plain int IDs, event type, and UTC timestamp are written.
///   <c>ModelUsed</c>, <c>ContextSource</c>, and <c>Intent</c> are NOT stored
///   (AI content fields are excluded — no prompt/response/model specifics in ActivityEvent).</item>
///   <item><b>Module isolation</b> — no project reference to <c>Ai.*</c> or <c>AiTutor.*</c>
///   module projects; subscribes via <c>Shared.Contracts.AiTutor</c> only.</item>
///   <item><b>SubjectCode / DurationSeconds</b> — null; the payload carries <c>SkillId</c>
///   (not <c>SubjectCode</c>) and no duration.</item>
/// </list>
/// </summary>
public sealed class HelpDeliveredEventHandler : INotificationHandler<HelpDeliveredIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public HelpDeliveredEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(HelpDeliveredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received HelpDeliveredIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"SkillId={notification.SkillId}).");

        var activityEvent = new ActivityEvent
        {
            StudentId       = notification.StudentId,
            EventType       = "AiHelpDelivered",
            SubjectCode     = null,   // payload carries SkillId, not SubjectCode (OQ-3)
            DurationSeconds = null,   // not carried on this event payload
            OccurredAtUtc   = notification.OccurredOnUtc,
            SourceEventId   = notification.EventId,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
