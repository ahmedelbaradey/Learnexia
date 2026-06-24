using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Commands.ApproveIngestionReviewItem;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Dtos;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.IngestionReview.Commands;

/// <summary>
/// Handles <see cref="ApproveIngestionReviewItemCommand"/> (BL-05-BE-8).
///
/// <para>Flow:
/// <list type="number">
///   <item>Load the item (404 if missing).</item>
///   <item>Guard: item must be Pending (409 if already resolved).</item>
///   <item>Open a single explicit transaction.</item>
///   <item>Call <see cref="IPedagogicalTreeWriter"/> to commit the node to the learning module
///         INSIDE the transaction.</item>
///   <item>Stamp item as Approved + ReviewedByUserId + ReviewedAt + ReviewNotes.</item>
///   <item>Commit — both the tree-write and the status stamp succeed or neither does.</item>
/// </list>
/// </para>
///
/// <para>
/// Finding #3 fix: the tree-write and the status stamp are now a single atomic operation.
/// If the tree-write throws, the transaction is rolled back and Approved is NOT stamped.
/// The error is surfaced to the caller (localized) and logged via ILoggerManager.
/// </para>
///
/// <para>No Unit of Work — explicit transaction (CLAUDE.md rule 3).</para>
/// </summary>
public sealed class ApproveIngestionReviewItemCommandHandler
    : BaseResponseHandler,
      ICommandHandler<ApproveIngestionReviewItemCommand, BaseResponse<IngestionReviewItemDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPedagogicalTreeWriter _treeWriter;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public ApproveIngestionReviewItemCommandHandler(
        CurriculumDbContext db,
        ICurrentUserService currentUserService,
        IPedagogicalTreeWriter treeWriter,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                 = db;
        _currentUserService = currentUserService;
        _treeWriter         = treeWriter;
        _mapper             = mapper;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<IngestionReviewItemDto>> Handle(
        ApproveIngestionReviewItemCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<IngestionReviewItemDto>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            // ── Step 1: Load item ────────────────────────────────────────────────────────────────
            var item = await _db.IngestionReviewItems
                .Include(r => r.CurriculumDocument)
                .FirstOrDefaultAsync(r => r.Id == request.ReviewItemId, cancellationToken);

            if (item is null)
                return NotFound<IngestionReviewItemDto>(
                    _localizer[SharedResourcesKey.IngestionReviewItemNotFound]);

            // ── Step 2: Guard — must be Pending ──────────────────────────────────────────────────
            if (item.Status != ReviewStatus.Pending)
                return Conflict<IngestionReviewItemDto>(
                    _localizer[SharedResourcesKey.IngestionReviewItemAlreadyResolved]);

            // ── Step 3: Parse PayloadJson (fields use Python contract names) ──────────────────────
            // PayloadJson is written by IngestJobAdvanceService using Python field names.
            var payloadInfo = ParsePayloadJson(item.PayloadJson);

            if (payloadInfo is null)
            {
                _logger.LogInfo(
                    $"ApproveIngestionReviewItem: PayloadJson for item id={item.Id} missing " +
                    "title/skill_key; cannot perform tree-write.");
                return BadRequest<IngestionReviewItemDto>(
                    _localizer[SharedResourcesKey.IngestionReviewItemTreeWriteFailed]);
            }

            // ── Step 4 + 5: Tree-write AND status-stamp in a single atomic transaction ───────────
            // Finding #3 fix: both succeed or neither does.
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            var document = item.CurriculumDocument;
            var gradeId  = document?.GradeId ?? 0;

            // EnsureSubjectAsync (idempotent).
            var subjectId = await _treeWriter.EnsureSubjectAsync(
                gradeId,
                payloadInfo.SubjectCode,
                (int)(document?.Language ?? 0),
                payloadInfo.SubjectDisplayName,
                cancellationToken);

            // EnsureConceptAsync — use title as concept name (distinct from skill name).
            var conceptId = await _treeWriter.EnsureConceptAsync(
                payloadInfo.ConceptName,
                subjectId,
                null,
                payloadInfo.Difficulty,
                cancellationToken);

            // EnsureSkillAsync.
            await _treeWriter.EnsureSkillAsync(
                payloadInfo.SkillName,
                conceptId,
                payloadInfo.SkillKey,
                masteryThreshold: 75,
                estimatedTimeMinutes: 20,
                subjectId,
                gradeId,
                payloadInfo.Difficulty,
                cancellationToken);

            _logger.LogInfo(
                $"ApproveIngestionReviewItem: tree-write succeeded for item id={item.Id} " +
                $"skillKey='{payloadInfo.SkillKey}'.");

            // Stamp as Approved only after the tree-write succeeds.
            item.Status           = ReviewStatus.Approved;
            item.ReviewedByUserId = userId.Value;
            item.ReviewedAt       = DateTimeOffset.UtcNow;
            item.ReviewNotes      = request.ReviewNotes;

            await _db.SaveChangesAsync(userId.Value);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInfo(
                $"BL-05 ApproveIngestionReviewItem: item id={item.Id} approved by user={userId}.");

            var response = Success(_mapper.Map<IngestionReviewItemDto>(item));
            response.Message = _localizer[SharedResourcesKey.IngestionReviewItemApproved];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ApproveIngestionReviewItemCommand");
            return ServerError<IngestionReviewItemDto>(
                _localizer[SharedResourcesKey.CurriculumUploadFailed]);
        }
    }

    // ── PayloadJson parsing ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parses the minimal fields needed for <see cref="IPedagogicalTreeWriter"/> from
    /// <c>PayloadJson</c>. The JSON is written by <c>IngestJobAdvanceService</c> using the
    /// Python contract field names (node_type / title / skill_key / subject_code / grade /
    /// difficulty / parent_skill_key).
    /// Returns <c>null</c> if the required fields (title, skill_key) are absent.
    /// </summary>
    private static ApproveNodePayload? ParsePayloadJson(string? payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
            return null;

        try
        {
            using var doc  = System.Text.Json.JsonDocument.Parse(payloadJson);
            var root       = doc.RootElement;

            // Python contract: "title" (not "name")
            var skillName = root.TryGetProperty("title", out var nm) && nm.ValueKind == System.Text.Json.JsonValueKind.String
                ? nm.GetString()
                : null;

            // Python contract: "skill_key"
            var skillKey = root.TryGetProperty("skill_key", out var sk) && sk.ValueKind == System.Text.Json.JsonValueKind.String
                ? sk.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(skillName) || string.IsNullOrWhiteSpace(skillKey))
                return null;

            // Python contract: "subject_code" is an int in the serialized payload
            // (IngestJobAdvanceService.SerializeNodeAsPayload stores the mapped int)
            var subjectCode = root.TryGetProperty("subject_code", out var sc) && sc.ValueKind == System.Text.Json.JsonValueKind.Number
                ? (sc.TryGetInt32(out var sci) ? sci : 0)
                : 0;

            var difficulty = root.TryGetProperty("difficulty", out var di) && di.ValueKind == System.Text.Json.JsonValueKind.Number
                ? (di.TryGetInt32(out var dii) ? dii : 2)
                : 2;

            // parent_skill_key: used as the concept name for the concept→skill path.
            var parentSkillKey = root.TryGetProperty("parent_skill_key", out var pk) && pk.ValueKind == System.Text.Json.JsonValueKind.String
                ? pk.GetString()
                : null;

            // Concept name = parent_skill_key if present, else fall back to skill title.
            var conceptName = !string.IsNullOrWhiteSpace(parentSkillKey) ? parentSkillKey : skillName;

            // subject_display_name is used only for EnsureSubjectAsync display; derive from subjectCode.
            var subjectDisplayName = subjectCode switch
            {
                0 => "Math",
                1 => "Science",
                2 => "Arabic",
                3 => "English",
                _ => "Math",
            };

            return new ApproveNodePayload(
                SkillName:          skillName!,
                SkillKey:           skillKey!,
                SubjectCode:        subjectCode,
                SubjectDisplayName: subjectDisplayName,
                ConceptName:        conceptName!,
                Difficulty:         difficulty);
        }
        catch
        {
            return null;
        }
    }

    private sealed record ApproveNodePayload(
        string SkillName,
        string SkillKey,
        int    SubjectCode,
        string SubjectDisplayName,
        string ConceptName,
        int    Difficulty);
}
