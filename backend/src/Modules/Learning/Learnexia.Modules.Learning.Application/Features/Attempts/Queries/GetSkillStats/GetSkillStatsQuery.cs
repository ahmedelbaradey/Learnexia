using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetSkillStats;

/// <summary>
/// Returns per-student, per-skill aggregated statistics from StudentAnswer rows.
/// Queries are NOT auto-validated — validation is performed inline in the handler.
/// StudentId is supplied via query string and checked against the JWT (IDOR guard).
/// Zero-data case returns a zeroed SkillStatsDto (not 404).
/// </summary>
public record GetSkillStatsQuery : IQuery<BaseResponse<SkillStatsDto>>
{
    public int SkillId { get; set; }
    public int StudentId { get; set; }
}
