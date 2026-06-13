using Learnexia.Modules.Learning.Application.Features.Mastery.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Mastery.Queries.GetSkillMastery;

/// <summary>
/// Returns the mastery row for a single skill, for the student identified by the authenticated JWT.
/// StudentId is resolved server-side — NEVER from the request (IDOR guard).
/// If no mastery row exists for the (student, skill) pair, a NotStarted default is returned (never 500).
/// Query — not auto-validated by ValidationBehavior (CLAUDE rule 4).
/// </summary>
public record GetSkillMasteryQuery : IQuery<BaseResponse<MasteryDto>>
{
    /// <summary>The skill whose mastery is requested.</summary>
    public int SkillId { get; init; }
}
