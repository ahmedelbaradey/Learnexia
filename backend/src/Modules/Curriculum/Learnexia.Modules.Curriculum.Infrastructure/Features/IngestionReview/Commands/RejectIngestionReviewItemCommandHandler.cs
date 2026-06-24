using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Commands.RejectIngestionReviewItem;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Dtos;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.IngestionReview.Commands;

/// <summary>
/// Handles <see cref="RejectIngestionReviewItemCommand"/> (BL-05-BE-8).
///
/// <para>Flow:
/// <list type="number">
///   <item>Load the item (404 if missing).</item>
///   <item>Guard: item must be Pending (409 if already resolved).</item>
///   <item>Stamp item as Rejected + ReviewedByUserId + ReviewedAt + ReviewNotes.</item>
///   <item>Commit in an explicit transaction.</item>
/// </list>
/// </para>
///
/// <para>Rejected items are permanently withheld. To regenerate, re-ingest the document
/// (<c>ReIngestCurriculumDocumentCommand</c>).</para>
///
/// <para>No Unit of Work — explicit transaction (CLAUDE.md rule 3).</para>
/// </summary>
public sealed class RejectIngestionReviewItemCommandHandler
    : BaseResponseHandler,
      ICommandHandler<RejectIngestionReviewItemCommand, BaseResponse<IngestionReviewItemDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public RejectIngestionReviewItemCommandHandler(
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

    public async Task<BaseResponse<IngestionReviewItemDto>> Handle(
        RejectIngestionReviewItemCommand request,
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
                .FirstOrDefaultAsync(r => r.Id == request.ReviewItemId, cancellationToken);

            if (item is null)
                return NotFound<IngestionReviewItemDto>(
                    _localizer[SharedResourcesKey.IngestionReviewItemNotFound]);

            // ── Step 2: Guard — must be Pending ──────────────────────────────────────────────────
            if (item.Status != ReviewStatus.Pending)
                return Conflict<IngestionReviewItemDto>(
                    _localizer[SharedResourcesKey.IngestionReviewItemAlreadyResolved]);

            // ── Step 3: Stamp as Rejected ────────────────────────────────────────────────────────
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            item.Status           = ReviewStatus.Rejected;
            item.ReviewedByUserId = userId.Value;
            item.ReviewedAt       = DateTimeOffset.UtcNow;
            item.ReviewNotes      = request.ReviewNotes;

            await _db.SaveChangesAsync(userId.Value);
            await tx.CommitAsync(cancellationToken);

            _logger.LogInfo(
                $"BL-05 RejectIngestionReviewItem: item id={item.Id} rejected by user={userId}.");

            var response = Success(_mapper.Map<IngestionReviewItemDto>(item));
            response.Message = _localizer[SharedResourcesKey.IngestionReviewItemRejected];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in RejectIngestionReviewItemCommand");
            return ServerError<IngestionReviewItemDto>(
                _localizer[SharedResourcesKey.CurriculumUploadFailed]);
        }
    }
}
