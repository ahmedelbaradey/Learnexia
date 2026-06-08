using Learnexia.Modules.Learning.Application.Features.Questions.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Add;

/// <summary>
/// Adds a new quiz question to a lesson.
/// SequenceOrder is set by the handler (append: max existing + 1).
/// IsActive defaults to true (entity default — not mapped from command input).
/// </summary>
public record AddQuestionCommand : AddQuestionDto, ICommand<BaseResponse<string>>
{
}
