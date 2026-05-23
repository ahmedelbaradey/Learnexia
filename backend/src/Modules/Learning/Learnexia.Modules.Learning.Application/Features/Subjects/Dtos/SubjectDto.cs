using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

public record SubjectDto : BaseDto
{
    public string Name { get; set; } = null!;
    public string? Country { get; set; }
    public int GradeId { get; set; }
}
