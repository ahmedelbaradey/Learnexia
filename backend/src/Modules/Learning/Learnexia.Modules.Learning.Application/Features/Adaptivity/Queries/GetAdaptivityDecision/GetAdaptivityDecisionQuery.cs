using Learnexia.Modules.Learning.Application.Features.Adaptivity.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Adaptivity.Queries.GetAdaptivityDecision;

/// <summary>
/// Returns the current adaptive difficulty decision for the authenticated student and a given skill.
/// StudentId is resolved server-side from <c>ICurrentUserService.UserId</c> — NEVER from the request (IDOR guard).
/// Query — not auto-validated by ValidationBehavior (CLAUDE rule 4).
/// </summary>
public record GetAdaptivityDecisionQuery : IQuery<BaseResponse<AdaptivityDecisionDto>>
{
    /// <summary>The skill for which the adaptive difficulty is requested.</summary>
    public int SkillId { get; init; }
}
