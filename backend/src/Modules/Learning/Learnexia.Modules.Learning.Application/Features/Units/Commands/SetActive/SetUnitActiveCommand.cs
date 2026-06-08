using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.SetActive;

/// <summary>
/// Sets Unit.IsActive to <see cref="IsActive"/>.
/// Inactive units are hidden from student-facing reads but preserved for admin reads.
/// </summary>
public record SetUnitActiveCommand : ICommand<BaseResponse<string>>
{
    public int UnitId { get; init; }

    /// <summary>true to activate, false to deactivate.</summary>
    public bool IsActive { get; init; }
}
