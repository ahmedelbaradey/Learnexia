using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Units.Dtos;

/// <summary>
/// Admin-facing unit DTO.
/// P7-01: Added IsActive so admin can see the active/inactive state.
/// </summary>
public record UnitDto : BaseDto
{
    public string Name { get; set; } = null!;
    public int SequenceOrder { get; set; }
    public int SubjectId { get; set; }

    // P7-01: admin active/inactive toggle
    public bool IsActive { get; set; }
}
