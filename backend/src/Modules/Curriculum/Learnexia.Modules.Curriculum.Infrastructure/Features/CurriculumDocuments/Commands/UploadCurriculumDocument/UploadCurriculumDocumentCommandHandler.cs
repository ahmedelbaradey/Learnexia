using System.Text.Json;
using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.UploadCurriculumDocument;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Validators;
using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Configuration;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Abstractions.Storage;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.CurriculumDocuments.Commands.UploadCurriculumDocument;

/// <summary>
/// Handles <see cref="UploadCurriculumDocumentCommand"/> (BL-01 BE-4 + BE-5).
///
/// <para><strong>Mirrors <c>UploadAvatarCommandHandler</c> exactly:</strong>
/// validation order, 422 on invalid file, <c>ServerError&lt;T&gt;</c> on exception,
/// stream-based upload via <see cref="IStorageService"/>, store key not URL.</para>
///
/// <para>Validation order (fail-fast, 422 on every file rejection):
/// <list type="number">
///   <item>File presence/size (null or empty → 422; over cap → 422).</item>
///   <item>Declared content-type allow-list (not in set → 422).</item>
///   <item>Magic-byte signature detection (not a known type → 422).</item>
///   <item>Detected type resolution (detected CT is authoritative; declared CT is only a pre-filter — no CT↔detected cross-check is enforced; see BL-01 finding #2).</item>
/// </list>
/// </para>
///
/// <para><strong>Upload:</strong> file is STREAMED to storage (do not buffer in memory — AC3 / Q3).
/// Object key is a GUID-based string (no user-controlled filename — no path traversal).
/// The detected content-type (not the client-declared one) is stored with the object.</para>
///
/// <para><strong>BE-5 — DB-outbox (Q7):</strong>
/// After streaming, the <see cref="CurriculumDocument"/> row and a <see cref="PipelineJob"/>
/// row (<c>JobType='parse'</c>, <c>Status='Pending'</c>) are written inside an explicit
/// <c>BeginTransactionAsync</c> so a job-write failure rolls back the document insert.
/// No Unit of Work (CLAUDE.md rule 3). No MediatR integration event (MediatR is in-process;
/// the Python poller reads the DB-outbox row directly).</para>
/// </summary>
public sealed class UploadCurriculumDocumentCommandHandler
    : BaseResponseHandler,
      ICommandHandler<UploadCurriculumDocumentCommand, BaseResponse<CurriculumDocumentResponse>>
{
    private readonly CurriculumDbContext _db;
    private readonly IStorageService _storageService;
    private readonly CurriculumUploadConfiguration _uploadConfig;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UploadCurriculumDocumentCommandHandler(
        CurriculumDbContext db,
        IStorageService storageService,
        IOptions<CurriculumUploadConfiguration> uploadConfig,
        ICurrentUserService currentUserService,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                = db;
        _storageService    = storageService;
        _uploadConfig      = uploadConfig.Value;
        _currentUserService = currentUserService;
        _mapper            = mapper;
        _localizer         = localizer;
        _logger            = logger;
    }

    public async Task<BaseResponse<CurriculumDocumentResponse>> Handle(
        UploadCurriculumDocumentCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            var file = request.File;

            // ── Step 1: Presence / size ───────────────────────────────────────────────────────────
            // Invalid-file rejections return 422 (UnprocessableEntity) — never 500.
            if (file is null || file.Length == 0)
                return UnprocessableEntity<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumFileRequired]);

            if (file.Length > _uploadConfig.MaxFileSizeBytes)
                return UnprocessableEntity<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumFileTooLarge]);

            // ── Step 2: Declared content-type allow-list ──────────────────────────────────────────
            if (!CurriculumFileValidator.IsAllowedContentType(file.ContentType))
                return UnprocessableEntity<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumFileInvalidType]);

            // ── Step 3: Magic-byte detection (defends against spoofed content-type/extension) ─────
            // Ordered BEFORE storage — a file never reaches the bucket until its signature is verified.
            var detected = await CurriculumFileValidator.DetectByMagicBytesAsync(file, cancellationToken);
            if (detected is null)
                return UnprocessableEntity<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumFileInvalidMagicBytes]);

            // ── Step 4: Resolve content-type and extension from detected type ──────────────────
            // The DETECTED magic-byte type is authoritative: we derive content-type and extension
            // from it — the client-declared content-type is ONLY used as an allow-list pre-filter
            // (Step 2 above) and is otherwise ignored here. No CT↔detected cross-check is performed;
            // the detected type silently overrides whatever the client declared, so a spoofed
            // declared MIME is corrected, not rejected. The residual DOCX/zip risk (any PK-header file
            // passes as DOCX) is accepted for BL-01 and deferred to the Python parser (BL-02).
            // See BL-01 security audit finding #2 for the documented rationale.
            var detectedContentType = CurriculumFileValidator.GetContentType(detected.Value);
            var extension           = CurriculumFileValidator.GetExtension(detected.Value);

            // ── Step 5: Stream to storage under a GUID key ────────────────────────────────────────
            // Object key: <guid><extension> — no user-controlled filename, no path traversal.
            // Content-type: the TRUE magic-byte-detected type (not the client-declared MIME).
            // The file is STREAMED (no full-file buffer): IFormFile.OpenReadStream() is lazy.
            var objectKey = $"{Guid.NewGuid():N}{extension}";

            await using var uploadStream = file.OpenReadStream();
            var result = await _storageService.UploadFileAsync(
                uploadStream,
                objectKey,
                detectedContentType,
                file.Length,
                _uploadConfig.BucketName,
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogError(null,
                    $"BL-01 CurriculumDocument upload failed for user {userId}: {result.ErrorMessage}");
                return ServerError<CurriculumDocumentResponse>(
                    _localizer[SharedResourcesKey.CurriculumUploadFailed]);
            }

            // ── Steps 6-7 (BE-5): Persist CurriculumDocument + PipelineJob in a transaction ───────
            // Q7: both rows inside BeginTransactionAsync — job failure rolls back the document.
            // No Unit of Work (CLAUDE.md rule 3). No MediatR event (Python reads the DB-outbox row).
            // SaveChangesAsync(userId) stamps CreatedAt/CreatedBy via the audit override.
            await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var document = new CurriculumDocument
            {
                FileName    = file.FileName,
                ObjectKey   = result.FilePath,
                ContentType = detectedContentType,
                FileSize    = file.Length,
                GradeId     = request.GradeId,
                SubjectId   = request.SubjectId,
                Language    = request.Language,
                Country     = request.Country,
                Status      = DocumentStatus.Received,
            };
            _db.CurriculumDocuments.Add(document);
            await _db.SaveChangesAsync(userId.Value);

            // BE-5 — DB-outbox: enqueue parse job for the Python poller.
            // PayloadJson carries only object_key + content_type — no secrets, no unsanitized input.
            var payloadJson = JsonSerializer.Serialize(new
            {
                object_key   = result.FilePath,
                content_type = detectedContentType,
            });

            var job = new PipelineJob
            {
                JobType     = "parse",
                Status      = "Pending",
                DocumentId  = document.Id,
                PayloadJson = payloadJson,
                RetryCount  = 0,
            };
            _db.PipelineJobs.Add(job);
            await _db.SaveChangesAsync(userId.Value);

            await transaction.CommitAsync(cancellationToken);

            _logger.LogInfo(
                $"BL-01 CurriculumDocument id={document.Id} uploaded by user={userId}, " +
                $"objectKey={objectKey}, jobId={job.Id}.");

            var response = Created(_mapper.Map<CurriculumDocumentResponse>(document));
            response.Message = _localizer[SharedResourcesKey.CurriculumDocumentUploadedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UploadCurriculumDocumentCommand");
            return ServerError<CurriculumDocumentResponse>(
                _localizer[SharedResourcesKey.CurriculumUploadFailed]);
        }
    }
}
