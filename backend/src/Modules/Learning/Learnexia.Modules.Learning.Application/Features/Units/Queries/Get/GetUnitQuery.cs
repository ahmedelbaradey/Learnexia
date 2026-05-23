using Learnexia.Modules.Learning.Application.Features.Units.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Queries.Get;

public record GetUnitQuery : IQuery<BaseResponse<SingleUnitResponse>>
{
    public int Id { get; set; }
}
