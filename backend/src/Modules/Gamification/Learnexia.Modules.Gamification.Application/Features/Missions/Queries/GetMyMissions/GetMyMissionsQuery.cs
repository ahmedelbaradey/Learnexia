using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.Queries.GetMyMissions;

/// <summary>
/// Returns the daily + weekly missions for the currently authenticated student (P4-06, AC2 + AC3).
/// StudentId is resolved from <c>ICurrentUserService.UserId</c> inside the handler —
/// no client-supplied identity parameter (IDOR-proof by construction). Mirrors <see cref="GetMyBadgesQuery"/>.
///
/// This is a QUERY (read-only) — <c>IQuery&lt;T&gt;</c>, NOT <c>ICommand&lt;T&gt;</c>.
/// <c>ValidationBehavior</c> does NOT apply (rule 4). No fluent validator needed.
/// </summary>
public sealed record GetMyMissionsQuery : IQuery<BaseResponse<MyMissionsResponse>>;
