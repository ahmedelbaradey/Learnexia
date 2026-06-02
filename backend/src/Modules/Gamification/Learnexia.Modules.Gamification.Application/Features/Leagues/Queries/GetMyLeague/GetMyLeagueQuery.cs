using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Queries.GetMyLeague;

/// <summary>
/// Returns the authenticated student's full league standings for the current week (P4-07, AC3).
///
/// <para>Route: <c>GET /api/Gamification/Leagues/Me</c> — see <c>LeaguesController</c>.</para>
///
/// <para>StudentId is resolved exclusively from the JWT (<c>ICurrentUserService.UserId</c>) —
/// NEVER from a client-supplied parameter. IDOR is structurally impossible.</para>
///
/// <para>This is a READ-ONLY query (<see cref="IQuery{TResult}"/>).
/// <c>ValidationBehavior</c> does NOT run on queries (CLAUDE.md rule 4).
/// <c>UnitOfWorkBehavior</c> does NOT wrap this query — any lazy-placement writes
/// inside <c>IStudentLeagueQuery.GetByStudentIdAsync</c> commit in their own scope.</para>
/// </summary>
public sealed record GetMyLeagueQuery() : IQuery<BaseResponse<MyLeagueResponse>>;
