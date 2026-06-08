using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.SetActive;

/// <summary>
/// Sets Subject.IsActive to <see cref="IsActive"/>.
/// Inactive subjects are hidden from student-facing reads but preserved for admin reads.
/// </summary>
public record SetSubjectActiveCommand : ICommand<BaseResponse<string>>
{
    public int SubjectId { get; init; }

    /// <summary>true to activate, false to deactivate.</summary>
    public bool IsActive { get; init; }
}
