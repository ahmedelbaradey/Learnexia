using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="HintUsedIntegrationEvent"/> (emitted fire-and-forget
/// when a hint or why-wrong explanation is successfully delivered to a student).
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>AiHintUsed</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Note: <see cref="HintUsedIntegrationEvent"/> is already wired (produced by the Ai module)
/// and consumed by Learning's <c>HintUsedIntegrationEventHandler</c> (which increments
/// <c>StudentAnswer.HintUsed</c> and <c>Attempt.HintsUsedCount</c>). This Analytics consumer
/// is a second, independent consumer on the same notification — MediatR fan-out delivers to both.</para>
///
/// <para>Design constraints (P5-03 brief §Capture consumers):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — dedup on <see cref="HintUsedIntegrationEvent.EventId"/>
///   via the store's AnyAsync fast-path + unique-index guard.</item>
///   <item><b>Fail-soft</b> — the store swallows all persistence exceptions; never propagates.</item>
///   <item><b>PII-light</b> — only plain int IDs, event type, and UTC timestamp are written.
///   <c>QuestionId</c>, <c>AttemptId</c>, and <c>HintLevel</c> are NOT stored.</item>
///   <item><b>Module isolation</b> — no project reference to <c>Ai.*</c> or <c>AiTutor.*</c>
///   module projects; subscribes via <c>Shared.Contracts.AiTutor</c> only.</item>
///   <item><b>SubjectCode / DurationSeconds</b> — null; the payload carries neither.</item>
/// </list>
/// </summary>
public sealed class HintUsedEventHandler : INotificationHandler<HintUsedIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public HintUsedEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(HintUsedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received HintUsedIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"QuestionId={notification.QuestionId}, HintLevel={notification.HintLevel}).");

        var activityEvent = new ActivityEvent
        {
            StudentId       = notification.StudentId,
            EventType       = "AiHintUsed",
            SubjectCode     = null,   // not carried on this event payload
            DurationSeconds = null,   // not carried on this event payload
            OccurredAtUtc   = notification.OccurredOnUtc,
            SourceEventId   = notification.EventId,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
