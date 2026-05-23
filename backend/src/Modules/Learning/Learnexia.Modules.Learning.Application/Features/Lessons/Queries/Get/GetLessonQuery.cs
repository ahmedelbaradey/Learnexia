using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Queries.Get;

public record GetLessonQuery : IQuery<BaseResponse<SingleLessonResponse>>
{
    public int Id { get; set; }
}
