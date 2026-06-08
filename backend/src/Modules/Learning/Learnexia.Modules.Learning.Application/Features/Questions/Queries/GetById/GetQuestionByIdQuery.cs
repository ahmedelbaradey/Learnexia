using Learnexia.Modules.Learning.Application.Features.Questions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Queries.GetById;

/// <summary>
/// Returns a single quiz question by Id for admin authoring.
/// Returns <see cref="AdminQuestionDto"/> including CorrectAnswer (admin only).
/// </summary>
public record GetQuestionByIdQuery : IQuery<BaseResponse<AdminQuestionDto>>
{
    public int Id { get; init; }
}
