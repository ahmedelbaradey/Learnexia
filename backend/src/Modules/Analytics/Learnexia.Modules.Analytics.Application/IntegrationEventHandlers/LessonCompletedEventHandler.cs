using Learnexia.Modules.Analytics.Application.Abstractions;
using Learnexia.Modules.Analytics.Domain.Entities;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Analytics.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="LessonCompletedIntegrationEvent"/> (produced by the
/// Learning module's <c>CompleteAttemptCommandHandler</c> post-commit).
/// Appends one <see cref="ActivityEvent"/> (EventType = <c>LessonCompleted</c>) via
/// <see cref="IActivityEventStore"/> (Option C, idempotent, fail-soft).
///
/// <para>Design constraints (P5-03 brief §Capture consumers):</para>
/// <list type="bullet">
///   <item><b>Idempotent</b> — the store skips if a row with the same
///   <see cref="LessonCompletedIntegrationEvent.EventId"/> already exists (fast-path AnyAsync check
///   + unique index guard), so redelivery never duplicates a row.</item>
///   <item><b>Fail-soft</b> — the store swallows persistence exceptions; this handler never
///   throws to the publisher. The <c>IsolatedNotificationPublisher</c> at the Host already
///   isolates per-handler failures, but the store's inner try/catch is the explicit guarantee.</item>
///   <item><b>PII-light</b> — only plain int IDs, event type, and UTC timestamp are written;
///   no names, emails, prompt/response text, or answer content.</item>
///   <item><b>Module isolation</b> — this handler subscribes via <c>Shared.Contracts</c> only;
///   no project reference to <c>Learning.*</c>.</item>
///   <item><b>SubjectCode / DurationSeconds</b> — null in v1; <c>LessonCompletedIntegrationEvent</c>
///   carries <c>SkillId</c>/<c>LessonId</c> but no subject code and no server-authoritative
///   duration (OQ-3 / OQ-4 resolution: subject breakdown stays in Learning's BySubject seam).</item>
/// </list>
/// </summary>
public sealed class LessonCompletedEventHandler : INotificationHandler<LessonCompletedIntegrationEvent>
{
    private readonly IActivityEventStore _store;
    private readonly ILoggerManager _logger;

    public LessonCompletedEventHandler(IActivityEventStore store, ILoggerManager logger)
    {
        _store  = store;
        _logger = logger;
    }

    public async Task Handle(LessonCompletedIntegrationEvent notification, CancellationToken cancellationToken)
    {
        _logger.LogInfo(
            $"Analytics: received LessonCompletedIntegrationEvent " +
            $"(EventId={notification.EventId}, StudentId={notification.StudentId}, " +
            $"LessonId={notification.LessonId}).");

        var activityEvent = new ActivityEvent
        {
            StudentId     = notification.StudentId,
            EventType     = "LessonCompleted",
            SubjectCode   = null,       // not carried on this event payload (OQ-3)
            DurationSeconds = null,     // not carried on this event payload (OQ-4)
            OccurredAtUtc = notification.OccurredOnUtc,
            SourceEventId = notification.EventId,
        };

        await _store.AddAsync(activityEvent, cancellationToken);
    }
}
