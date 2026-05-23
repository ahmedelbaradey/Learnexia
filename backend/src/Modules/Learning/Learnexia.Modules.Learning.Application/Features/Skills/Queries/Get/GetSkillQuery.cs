using Learnexia.Modules.Learning.Application.Features.Skills.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Queries.Get;

public record GetSkillQuery : IQuery<BaseResponse<SingleSkillResponse>>
{
    public int Id { get; set; }
}
