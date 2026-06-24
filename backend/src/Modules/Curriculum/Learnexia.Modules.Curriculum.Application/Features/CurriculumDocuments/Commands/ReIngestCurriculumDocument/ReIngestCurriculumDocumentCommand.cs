using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.ReIngestCurriculumDocument;

/// <summary>
/// Admin command to re-enqueue an ingest job for an existing <c>CurriculumDocument</c>
/// (BL-05-BE-9).
///
/// <para>Prerequisites:
/// <list type="bullet">
///   <item>Document must exist (404 otherwise).</item>
///   <item>Document must have successfully parsed (<c>ParsedArtifactObjectKey</c> not null)
///         so the ingest worker has an artifact to process.</item>
///   <item>No non-terminal ingest job in flight (Pending or Processing) — 409 if found.</item>
/// </list>
/// </para>
///
/// <para>On success: document <c>IngestionStatus</c> is reset to <c>InProgress</c> and a fresh
/// <c>Pending</c> ingest job is written inside an explicit transaction.
/// All existing <c>IngestionReviewItem</c> rows for the document are set to <c>Rejected</c>
/// so stale low-confidence items don't surface in the admin queue.</para>
/// </summary>
public record ReIngestCurriculumDocumentCommand(int DocumentId)
    : ICommand<BaseResponse<CurriculumDocumentResponse>>;
