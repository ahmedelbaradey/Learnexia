using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Delete;

/// <summary>
/// Soft-deletes a quiz question by setting <c>IsDeleted = true</c>.
/// The global query filter on QuizQuestion excludes deleted rows from all subsequent reads.
/// </summary>
public record DeleteQuestionCommand : ICommand<BaseResponse<string>>
{
    public int Id { get; init; }
}
