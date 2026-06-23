using Learnexia.Modules.Curriculum.Domain.Enums;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;

/// <summary>
/// Detail DTO for GET api/curriculum/documents/{id} (BL-01 BE-7).
///
/// <para><strong>Security note (flagged for security-auditor):</strong> <c>ObjectKey</c>
/// is included here for admins (full detail view). For non-admin callers (if ever widened)
/// replace with a presigned URL computed on read. The security-auditor gate should verify
/// that this endpoint is AdminOnly-gated before the R2 review passes.</para>
///
/// <para><c>StatusReason</c> is the human-readable failure diagnostic (null on non-Failed states).</para>
/// </summary>
public class CurriculumDocumentDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Storage object key (GUID-based). Returned only to admins.
    /// Security-auditor: confirm this endpoint is AdminOnly-gated (AC3).
    /// </summary>
    public string ObjectKey { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long FileSize { get; set; }
    public int GradeId { get; set; }
    public int SubjectId { get; set; }
    public ContentLanguage Language { get; set; }
    public string? Country { get; set; }
    public DocumentStatus Status { get; set; }
    public string? StatusReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
