using Learnexia.Modules.Curriculum.Domain.Enums;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;

/// <summary>
/// Paged-list DTO for GET api/curriculum/documents (BL-01 BE-6).
///
/// <para><strong>Security:</strong> <c>ObjectKey</c> is deliberately excluded (least exposure
/// principle). Admins retrieve the object key via the detail endpoint where presigning happens.</para>
/// </summary>
public class CurriculumDocumentListDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
    public int GradeId { get; set; }
    public int SubjectId { get; set; }
    public ContentLanguage Language { get; set; }
    public string? Country { get; set; }
    public DocumentStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
