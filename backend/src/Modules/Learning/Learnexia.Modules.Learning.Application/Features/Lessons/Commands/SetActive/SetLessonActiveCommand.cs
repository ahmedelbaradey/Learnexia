using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.SetActive;

/// <summary>
/// Sets Lesson.IsActive to <see cref="IsActive"/>.
/// Inactive lessons are hidden from student-facing reads but preserved for admin reads.
/// </summary>
public record SetLessonActiveCommand : ICommand<BaseResponse<string>>
{
    public int LessonId { get; init; }

    /// <summary>true to activate, false to deactivate.</summary>
    public bool IsActive { get; init; }
}
