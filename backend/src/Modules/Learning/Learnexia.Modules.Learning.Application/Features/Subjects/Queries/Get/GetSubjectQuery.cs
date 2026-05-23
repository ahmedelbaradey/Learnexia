using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.Get;

public record GetSubjectQuery : IQuery<BaseResponse<SingleSubjectResponse>>
{
    public int Id { get; set; }
}
