using Learnexia.Modules.Gamification.Application.Features.Leagues.Commands.IncrementLeagueXp;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.EventHandlers;

/// <summary>
/// Listens to <see cref="XpAwardedDomainEvent"/> — raised by
/// <c>StudentXpProfile.ApplyAward</c> on EVERY XP addition — and forwards to
/// <see cref="IncrementLeagueXpCommand"/> so the student's active
/// <c>LeagueMembership.WeeklyXp</c> counter is incremented.
///
/// Mirrors <c>LessonCompletedMissionHandler</c> shape exactly.
///
/// No-op paths (handled inside <c>IncrementLeagueXpCommandHandler</c>):
///   • Student has no profile yet.
///   • Student has no active membership for the current week (hasn't loaded the dashboard —
///     D15 accepted data-loss, Q6 locked decision).
///   • Duplicate event (idempotency pre-check + DB unique constraint backstop).
///
/// Idempotency note: <see cref="XpAwardedDomainEvent.EventId"/> is a fresh <c>Guid.NewGuid()</c>
/// assigned at domain-event construction time. Each invocation of this handler generates a new
/// <c>OriginEventId</c> from that stable <c>EventId</c>, passed directly to the command.
/// Because <c>IsolatedNotificationPublisher</c> (ADR 0002) does NOT retry on success, the same
/// domain event is delivered exactly once per handler per dispatch. The
/// <c>LeagueXpDeltaLog.UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId</c> unique index
/// guards against any crash-then-redelivery edge cases.
///
/// Fail-soft: the outer try/catch ensures a crash here does NOT propagate back to the upstream
/// XP/badge/mission handlers or roll back the originating write (ADR 0002 §3).
/// </summary>
public sealed class XpAwardedLeagueHandler : INotificationHandler<XpAwardedDomainEvent>
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public XpAwardedLeagueHandler(IMediator mediator, ILoggerManager logger)
    {
        _mediator = mediator;
        _logger   = logger;
    }

    public async Task Handle(XpAwardedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new IncrementLeagueXpCommand(
                StudentId:       notification.StudentId,
                Amount:          notification.Amount,
                OriginEventId:   notification.EventId,
                OriginEventType: nameof(XpAwardedDomainEvent),
                OccurredAtUtc:   notification.OccurredAtUtc), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-07: Error in XpAwardedLeagueHandler " +
                $"studentId={notification.StudentId} amount={notification.Amount}.");
        }
    }
}
