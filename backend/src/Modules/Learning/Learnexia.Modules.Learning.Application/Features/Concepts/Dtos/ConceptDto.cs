using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;

public record ConceptDto : BaseDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DifficultyLevel DifficultyLevel { get; set; }
    public int SubjectId { get; set; }
}
