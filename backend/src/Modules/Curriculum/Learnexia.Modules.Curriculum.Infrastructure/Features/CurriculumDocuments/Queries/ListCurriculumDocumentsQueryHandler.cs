using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Queries.ListCurriculumDocuments;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.CurriculumDocuments.Queries;

/// <summary>
/// Handles <see cref="ListCurriculumDocumentsQuery"/> — returns a paginated list of
/// curriculum documents, optionally filtered by status, grade, and subject (BL-01 BE-6).
///
/// <para>Uses AutoMapper <c>ProjectTo</c> for server-side projection. The query is ordered
/// by <c>CreatedAt DESC</c> so the most recent documents appear first.</para>
///
/// <para>Queries skip ValidationBehavior — this handler guards inputs itself.</para>
/// </summary>
public sealed class ListCurriculumDocumentsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<ListCurriculumDocumentsQuery, PaginatedResult<CurriculumDocumentListDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ListCurriculumDocumentsQueryHandler(
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

    public async Task<PaginatedResult<CurriculumDocumentListDto>> Handle(
        ListCurriculumDocumentsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = _db.CurriculumDocuments.AsNoTracking();

            if (request.Status.HasValue)
                query = query.Where(d => d.Status == request.Status.Value);

            if (request.GradeId.HasValue && request.GradeId.Value > 0)
                query = query.Where(d => d.GradeId == request.GradeId.Value);

            if (request.SubjectId.HasValue && request.SubjectId.Value > 0)
                query = query.Where(d => d.SubjectId == request.SubjectId.Value);

            // Order most-recent first (consistent admin queue view).
            var ordered = query.OrderByDescending(d => d.CreatedAt);

            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            var pageSize   = request.PageSize  <= 0 ? 20 : request.PageSize;

            var totalCount = await ordered.CountAsync(cancellationToken);

            if (totalCount == 0)
                return PaginatedResult<CurriculumDocumentListDto>.EmptyCollection(
                    _localizer[SharedResourcesKey.CurriculumDocumentsRetrievedSuccessfully]);

            var items = await ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ProjectTo<CurriculumDocumentListDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            var result = PaginatedResult<CurriculumDocumentListDto>.Success(
                items, totalCount, pageNumber, pageSize,
                _localizer[SharedResourcesKey.CurriculumDocumentsRetrievedSuccessfully]);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListCurriculumDocumentsQuery");
            return PaginatedResult<CurriculumDocumentListDto>.ServerError(
                _localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
