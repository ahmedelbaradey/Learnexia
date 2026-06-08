using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Shared.Kernel.Dtos;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;

/// <summary>
/// Admin-facing subject DTO. Exposes all curriculum admin fields.
/// P7-01: Added SubjectCode, Language, SequenceOrder, IsActive so the 6 roots/grade are
/// distinguishable. Student-facing reads use <see cref="StudentSubjectDto"/> — that contract
/// is not changed.
/// </summary>
public record SubjectDto : BaseDto
{
    public string Name { get; set; } = null!;
    public string? Country { get; set; }
    public int GradeId { get; set; }

    // P7-01: curriculum admin fields
    public SubjectCode SubjectCode { get; set; }
    public ContentLanguage Language { get; set; }
    public int SequenceOrder { get; set; }
    public bool IsActive { get; set; }
}
