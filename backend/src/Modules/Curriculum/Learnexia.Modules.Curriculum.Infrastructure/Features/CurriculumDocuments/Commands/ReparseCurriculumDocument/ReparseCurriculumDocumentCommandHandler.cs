using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.ReparseCurriculumDocument;
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

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.CurriculumDocuments.Commands.ReparseCurriculumDocument;

/// <summary>
/// Handles <see cref="ReparseCurriculumDocumentCommand"/> (BL-02-BE-4).
///
/// <para>Flow:
/// <list type="number">
///   <item>Load the document (404 if missing).</item>
///   <item>Check for any non-terminal parse job in flight (Pending or Processing) → 409 if found.</item>
///   <item>Inside an explicit transaction:
///     <list type="bullet">
///       <item>Reset <c>CurriculumDocument.Status = Processing</c> (document is re-parsing).</item>
///       <item>Insert a fresh <c>PipelineJob {JobType='parse', Status='Pending'}</c>
///             with the same payload as the original job (mirrors BL-01 BE-5 enqueue).</item>
///     </list>
///   </item>
///   <item>Return 200 with the updated document response.</item>
/// </list>
/// </para>
///
/// <para>No Unit of Work — explicit transaction (CLAUDE.md rule 3).</para>
/// </summary>
public sealed class ReparseCurriculumDocumentCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ReparseCurriculumDocumentCommand, BaseResponse<CurriculumDocumentResponse>>
{
    private readonly CurriculumDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ReparseCurriculumDocumentCommandHandler(
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
        ReparseCurriculumDocumentCommand request,
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

            // ── Step 2: Check for in-flight parse job (409 guard) ────────────────────────────────
            var hasInflightJob = await _db.PipelineJobs
                .AnyAsync(
                    j => j.DocumentId == request.DocumentId
                      && j.JobType    == "parse"
                      && (j.Status == "Pending" || j.Status == "Processing"),
                    cancellationToken);

            if (hasInflightJob)
                return Conflict<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumReparseJobInFlight]);

            // ── Step 3: Rebuild the payload from the document's stored ObjectKey ─────────────────
            // The payload mirrors BL-01 BE-5: {object_key, content_type}.
            // ObjectKey and ContentType are stored on the document from the original upload.
            var payloadJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                object_key   = document.ObjectKey,
                content_type = document.ContentType,
            });

            // ── Step 4: Transactional reset + enqueue ────────────────────────────────────────────
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            document.Status       = DocumentStatus.Processing;
            document.StatusReason = null;

            var freshJob = new PipelineJob
            {
                JobType     = "parse",
                Status      = "Pending",
                DocumentId  = document.Id,
                PayloadJson = payloadJson,
                RetryCount  = 0,
            };
            _db.PipelineJobs.Add(freshJob);

            await _db.SaveChangesAsync(userId.Value);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInfo(
                $"BL-02 ReparseCurriculumDocument: document id={document.Id} re-enqueued " +
                $"by user={userId}, newJobId={freshJob.Id}.");

            var response = Success(_mapper.Map<CurriculumDocumentResponse>(document));
            response.Message = _localizer[SharedResourcesKey.CurriculumReparseEnqueued];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ReparseCurriculumDocumentCommand");
            return ServerError<CurriculumDocumentResponse>(
                _localizer[SharedResourcesKey.CurriculumUploadFailed]);
        }
    }
}
