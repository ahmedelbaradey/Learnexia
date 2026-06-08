using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.SetActive;

/// <summary>
/// Toggles the <c>IsActive</c> flag on a quiz question.
/// Inactive questions are hidden from student quiz/attempt reads but are NOT deleted.
/// </summary>
public record SetQuestionActiveCommand : ICommand<BaseResponse<string>>
{
    public int Id { get; init; }

    /// <summary><c>true</c> to activate; <c>false</c> to deactivate.</summary>
    public bool IsActive { get; init; }
}
