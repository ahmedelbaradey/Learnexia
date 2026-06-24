using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Dtos;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Queries.GetIngestionReviewItems;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.IngestionReview.Queries;

/// <summary>
/// Handles <see cref="GetIngestionReviewItemsQuery"/> — returns a paginated list of
/// <c>IngestionReviewItem</c> rows for the admin review queue (BL-05-BE-7).
///
/// <para>Uses AutoMapper <c>ProjectTo</c> for server-side projection. Ordered by
/// <c>CreatedAt DESC</c> so the most-recently-created items appear first.</para>
/// </summary>
public sealed class GetIngestionReviewItemsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetIngestionReviewItemsQuery, PaginatedResult<IngestionReviewItemDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetIngestionReviewItemsQueryHandler(
        CurriculumDbContext db,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _db        = db;
        _mapper    = mapper;
        _logger    = logger;
        _localizer = localizer;
    }

    public async Task<PaginatedResult<IngestionReviewItemDto>> Handle(
        GetIngestionReviewItemsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = _db.IngestionReviewItems.AsNoTracking();

            if (request.DocumentId.HasValue && request.DocumentId.Value > 0)
                query = query.Where(r => r.CurriculumDocumentId == request.DocumentId.Value);

            if (request.Status.HasValue)
                query = query.Where(r => r.Status == request.Status.Value);
            else
                query = query.Where(r => r.Status == ReviewStatus.Pending);

            if (request.VersionId.HasValue && request.VersionId.Value > 0)
                query = query.Where(r => r.CurriculumVersionId == request.VersionId.Value);

            var ordered = query.OrderByDescending(r => r.CreatedAt);

            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            // Finding #4 fix: cap pageSize to prevent resource-exhaustion by an admin (or compromised token).
            var pageSize   = request.PageSize  <= 0 ? 20 : Math.Min(request.PageSize, 100);

            var totalCount = await ordered.CountAsync(cancellationToken);

            if (totalCount == 0)
                return PaginatedResult<IngestionReviewItemDto>.EmptyCollection(
                    _localizer[SharedResourcesKey.IngestionReviewItemsRetrievedSuccessfully]);

            var items = await ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ProjectTo<IngestionReviewItemDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            return PaginatedResult<IngestionReviewItemDto>.Success(
                items, totalCount, pageNumber, pageSize,
                _localizer[SharedResourcesKey.IngestionReviewItemsRetrievedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetIngestionReviewItemsQuery");
            return PaginatedResult<IngestionReviewItemDto>.ServerError(
                _localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
