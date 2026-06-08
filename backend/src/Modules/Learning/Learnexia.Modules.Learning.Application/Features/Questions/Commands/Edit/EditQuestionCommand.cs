using Learnexia.Modules.Learning.Application.Features.Questions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Edit;

/// <summary>
/// Edits an existing quiz question's type, text, options, correct answer, and difficulty.
/// IsActive, IsDeleted, SequenceOrder, and LessonId are NOT touched here — dedicated commands own those.
/// </summary>
public record EditQuestionCommand : EditQuestionDto, ICommand<BaseResponse<string>>
{
}
