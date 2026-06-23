using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Curriculum.Domain.Entities;

/// <summary>
/// The front-door entity for the Curriculum Intelligence pipeline.
/// Represents a single curriculum file uploaded by an admin (PDF, DOCX, or image).
/// (BL-01 BE-1, curriculum-system-of-record.md §4b)
///
/// Design rules:
/// - <see cref="GradeId"/> and <see cref="SubjectId"/> are plain ints — cross-module refs
///   (they live in the learning schema). No FK/nav. Module isolation enforced.
/// - <see cref="ObjectKey"/> is a GUID-based storage key, NEVER a URL.
/// - <see cref="Language"/> reuses the existing <see cref="ContentLanguage"/> enum.
/// - <see cref="Status"/> starts at <see cref="DocumentStatus.Received"/> on upload.
///   Stored as int — internal lifecycle, not readable by the Python poller.
/// - On upload success, a <see cref="PipelineJob"/> row (JobType='parse', Status='Pending')
///   is written to the outbox; that is the BL-02 trigger (not a MediatR event).
/// </summary>
public class CurriculumDocument : AggregateRoot
{
    /// <summary>
    /// Original filename as provided by the client (display only — never used as the storage key).
    /// </summary>
    public string FileName { get; set; } = null!;

    /// <summary>
    /// Storage object key (GUID-based). Written by the upload handler from IStorageService.FilePath.
    /// NEVER a URL — presign at the read layer.
    /// </summary>
    public string ObjectKey { get; set; } = null!;

    /// <summary>
    /// MIME type as detected by magic-byte inspection (not the client-declared value).
    /// e.g. "application/pdf", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
    ///      "image/jpeg", "image/png", "image/webp".
    /// </summary>
    public string ContentType { get; set; } = null!;

    /// <summary>
    /// File size in bytes.
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    /// Target grade. Plain int — cross-module ref to learning.Grades.Id. No FK/nav (module isolation).
    /// </summary>
    public int GradeId { get; set; }

    /// <summary>
    /// Target subject. Plain int — cross-module ref to learning.Subjects.Id. No FK/nav (module isolation).
    /// </summary>
    public int SubjectId { get; set; }

    /// <summary>
    /// Language of the document content. Reuses the existing <see cref="ContentLanguage"/> enum.
    /// Stored as int.
    /// </summary>
    public ContentLanguage Language { get; set; }

    /// <summary>
    /// Optional country context (e.g. "EG", "SA"). Nullable.
    /// </summary>
    public string? Country { get; set; }

    /// <summary>
    /// Current pipeline status for this document. Starts as <see cref="DocumentStatus.Received"/>.
    /// Stored as int (internal to .NET — NOT the cross-process string used in PipelineJob.Status).
    /// </summary>
    public DocumentStatus Status { get; set; } = DocumentStatus.Received;

    /// <summary>
    /// Human-readable reason for the current status, populated on failure.
    /// Null when Status is Received/Processing/Done.
    /// </summary>
    public string? StatusReason { get; set; }

    // Navigation — PipelineJobs for this document (intra-module, same schema).
    public ICollection<PipelineJob> PipelineJobs { get; set; } = new List<PipelineJob>();
}
