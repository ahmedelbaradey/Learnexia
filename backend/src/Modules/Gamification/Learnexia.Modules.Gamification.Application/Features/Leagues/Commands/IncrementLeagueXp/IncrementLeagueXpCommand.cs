using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Commands.IncrementLeagueXp;

/// <summary>
/// Increments the <c>WeeklyXp</c> counter on the student's active <c>LeagueMembership</c> for the
/// current week period.
///
/// Sent by <c>XpAwardedLeagueHandler</c> on every <c>XpAwardedDomainEvent</c>. Runs through
/// <c>UnitOfWorkBehavior</c> for an atomic write of the <c>LeagueXpDeltaLog</c> idempotency row
/// and the mutated <c>LeagueMembership.WeeklyXp</c> value.
///
/// Idempotency: two-layer defence —
///   (a) <see cref="Abstractions.IGamificationRepository.HasLeagueXpDeltaLogAsync"/> pre-check,
///   (b) DB unique index <c>UX_LeagueXpDeltaLogs_LeagueMembershipId_OriginEventId</c> catches races.
///
/// No-op when the student has no profile yet (brand-new account) or has not loaded the dashboard
/// this week (no active membership — D15 accepted data-loss decision).
/// </summary>
public sealed record IncrementLeagueXpCommand(
    int StudentId,
    int Amount,
    Guid OriginEventId,
    string OriginEventType,
    DateTime OccurredAtUtc) : ICommand<BaseResponse<Unit>>;
