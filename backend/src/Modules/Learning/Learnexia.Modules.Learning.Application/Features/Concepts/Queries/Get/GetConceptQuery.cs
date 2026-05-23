using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Queries.Get;

public record GetConceptQuery : IQuery<BaseResponse<SingleConceptResponse>>
{
    public int Id { get; set; }
}
