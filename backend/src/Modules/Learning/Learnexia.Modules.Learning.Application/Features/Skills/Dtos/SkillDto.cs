using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Skills.Dtos;

public record SkillDto : BaseDto
{
    public string Name { get; set; } = null!;
    public int MasteryThreshold { get; set; }
    public int EstimatedTimeMinutes { get; set; }
    public int ConceptId { get; set; }
}
