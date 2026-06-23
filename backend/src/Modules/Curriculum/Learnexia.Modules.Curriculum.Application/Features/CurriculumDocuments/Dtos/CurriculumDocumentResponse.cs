using Learnexia.Modules.Curriculum.Domain.Enums;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;

/// <summary>
/// Upload response DTO — returned as the Data payload in the 201 Created response
/// from POST api/curriculum/documents (BL-01 BE-4).
///
/// <para><strong>Security:</strong> <c>ObjectKey</c> is intentionally NOT included here.
/// The key is internal; callers should use the presigned GET from the detail endpoint.
/// Only the document id, metadata, and status are exposed.</para>
/// </summary>
public class CurriculumDocumentResponse
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
}
