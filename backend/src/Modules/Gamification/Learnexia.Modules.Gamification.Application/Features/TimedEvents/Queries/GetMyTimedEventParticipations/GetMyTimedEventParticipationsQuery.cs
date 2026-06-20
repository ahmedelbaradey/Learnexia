using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.TimedEvents.Queries.GetMyTimedEventParticipations;

/// <summary>
/// Returns the active timed-event participation snapshots for the currently authenticated student.
/// StudentId is resolved from <c>ICurrentUserService.UserId</c> inside the handler —
/// no client-supplied identity parameter (IDOR-proof by construction).
/// Mirrors <see cref="GetMyMissionsQuery"/> shape (P4-12, BE-8).
///
/// This is a QUERY (read-only) — <c>IQuery&lt;T&gt;</c>, NOT <c>ICommand&lt;T&gt;</c>.
/// <c>ValidationBehavior</c> does NOT apply (rule 4). No fluent validator needed.
/// </summary>
public sealed record GetMyTimedEventParticipationsQuery
    : IQuery<BaseResponse<IReadOnlyList<TimedEventParticipationSnapshot>>>;
