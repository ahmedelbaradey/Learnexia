using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Grades.Dtos;

public record GradeDto : BaseDto
{
    public int Number { get; set; }
    public string DisplayName { get; set; } = null!;
}
