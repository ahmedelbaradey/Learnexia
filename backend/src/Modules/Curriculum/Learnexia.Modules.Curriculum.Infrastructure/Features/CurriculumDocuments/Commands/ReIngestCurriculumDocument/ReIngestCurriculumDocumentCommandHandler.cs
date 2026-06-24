using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.ReIngestCurriculumDocument;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.CurriculumDocuments.Commands.ReIngestCurriculumDocument;

/// <summary>
/// Handles <see cref="ReIngestCurriculumDocumentCommand"/> (BL-05-BE-9).
///
/// <para>Flow:
/// <list type="number">
///   <item>Load the document (404 if missing).</item>
///   <item>Guard: document must have been successfully parsed (<c>ParsedArtifactObjectKey</c> not null);
///         returns 422 if not yet parsed.</item>
///   <item>Guard: no non-terminal ingest job must be in flight (409 if Pending/Processing).</item>
///   <item>Inside an explicit transaction:
///     <list type="bullet">
///       <item>Reset all existing Pending <c>IngestionReviewItem</c> rows for this document to
///             Rejected so stale low-confidence items don't surface in the admin queue.</item>
///       <item>Reset <c>CurriculumDocument.IngestionStatus = InProgress</c>.</item>
///       <item>Insert a fresh <c>PipelineJob{JobType='ingest', Status='Pending'}</c>
///             with the artifact key payload.</item>
///     </list>
///   </item>
///   <item>Return 200 with the updated document response.</item>
/// </list>
/// </para>
///
/// <para>No Unit of Work — explicit transaction (CLAUDE.md rule 3).</para>
/// </summary>
public sealed class ReIngestCurriculumDocumentCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ReIngestCurriculumDocumentCommand, BaseResponse<CurriculumDocumentResponse>>
{
    private readonly CurriculumDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ReIngestCurriculumDocumentCommandHandler(
        CurriculumDbContext db,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                 = db;
        _currentUserService = currentUserService;
        _mapper             = mapper;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<CurriculumDocumentResponse>> Handle(
        ReIngestCurriculumDocumentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            // ── Step 1: Load document ────────────────────────────────────────────────────────────
            var document = await _db.CurriculumDocuments
                .FirstOrDefaultAsync(d => d.Id == request.DocumentId, cancellationToken);

            if (document is null)
                return NotFound<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumDocumentNotFoundForReparse]);

            // ── Step 2: Guard — document must have been parsed ───────────────────────────────────
            if (string.IsNullOrWhiteSpace(document.ParsedArtifactObjectKey))
                return BadRequest<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumDocumentNotParsed]);

            // ── Step 3: Guard — no in-flight ingest job ──────────────────────────────────────────
            var hasInflightJob = await _db.PipelineJobs
                .AnyAsync(
                    j => j.DocumentId == request.DocumentId
                      && j.JobType    == "ingest"
                      && (j.Status == "Pending" || j.Status == "Processing"),
                    cancellationToken);

            if (hasInflightJob)
                return Conflict<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumIngestJobInFlight]);

            // ── Step 4: Transactional reset + enqueue ────────────────────────────────────────────
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            // Invalidate stale Pending review items so the admin queue doesn't show old entries.
            var stalePendingItems = await _db.IngestionReviewItems
                .Where(r => r.CurriculumDocumentId == request.DocumentId &&
                            r.Status == ReviewStatus.Pending)
                .ToListAsync(cancellationToken);

            foreach (var staleItem in stalePendingItems)
                staleItem.Status = ReviewStatus.Rejected;

            // Reset ingest status.
            document.IngestionStatus      = IngestionStatus.InProgress;
            document.IngestionDiagnostics = null;
            document.IngestedAt           = null;

            // Build ingest job payload from the stored artifact key.
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                artifact_key = document.ParsedArtifactObjectKey,
                document_id  = document.Id,
            });

            var freshJob = new PipelineJob
            {
                JobType     = "ingest",
                Status      = "Pending",
                DocumentId  = document.Id,
                PayloadJson = payloadJson,
                RetryCount  = 0,
            };
            _db.PipelineJobs.Add(freshJob);

            await _db.SaveChangesAsync(userId.Value);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInfo(
                $"BL-05 ReIngestCurriculumDocument: document id={document.Id} re-enqueued " +
                $"by user={userId}, newJobId={freshJob.Id}. " +
                $"Invalidated {stalePendingItems.Count} stale Pending review item(s).");

            var response = Success(_mapper.Map<CurriculumDocumentResponse>(document));
            response.Message = _localizer[SharedResourcesKey.CurriculumReIngestJobEnqueued];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReIngestCurriculumDocumentCommand");
            return ServerError<CurriculumDocumentResponse>(
                _localizer[SharedResourcesKey.CurriculumUploadFailed]);
        }
    }
}
