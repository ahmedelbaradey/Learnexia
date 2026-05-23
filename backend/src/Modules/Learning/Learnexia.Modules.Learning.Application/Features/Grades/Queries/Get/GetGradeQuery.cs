using Learnexia.Modules.Learning.Application.Features.Grades.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Queries.Get;

public record GetGradeQuery : IQuery<BaseResponse<SingleGradeResponse>>
{
    public int Id { get; set; }
}
