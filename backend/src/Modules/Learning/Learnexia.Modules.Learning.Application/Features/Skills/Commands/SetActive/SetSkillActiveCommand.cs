using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Commands.SetActive;

/// <summary>
/// Sets <see cref="Domain.Entities.Skill.IsActive"/> to <see cref="IsActive"/>.
/// Inactive skills are hidden from student-facing reads but preserved for admin reads.
/// </summary>
public record SetSkillActiveCommand : ICommand<BaseResponse<string>>
{
    public int SkillId { get; init; }

    /// <summary>true to activate, false to deactivate.</summary>
    public bool IsActive { get; init; }
}
