using Learnexia.Modules.Learning.Application.Features.Questions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Queries.ListForLesson;

/// <summary>
/// Returns all non-deleted quiz questions for a lesson (admin view — includes inactive).
/// Questions are ordered by SequenceOrder.
/// AdminOnly: returns <see cref="AdminQuestionDto"/> including CorrectAnswer.
/// </summary>
public record ListQuestionsForLessonQuery : IQuery<BaseResponse<List<AdminQuestionDto>>>
{
    public int LessonId { get; init; }
}
