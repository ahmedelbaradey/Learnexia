using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Units.Dtos;

public record UnitDto : BaseDto
{
    public string Name { get; set; } = null!;
    public int SequenceOrder { get; set; }
    public int SubjectId { get; set; }
}
