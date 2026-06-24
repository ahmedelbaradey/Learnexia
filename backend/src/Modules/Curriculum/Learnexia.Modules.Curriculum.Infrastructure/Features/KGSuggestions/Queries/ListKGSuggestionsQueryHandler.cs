using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Queries.ListKGSuggestions;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Curriculum.Infrastructure.Features.KGSuggestions.Queries;

/// <summary>
/// Handles <see cref="ListKGSuggestionsQuery"/> — returns a paginated list of
/// <c>KGSuggestion</c> rows for the admin review queue (BL-03-BE-7).
///
/// <para>Uses AutoMapper <c>ProjectTo</c> for server-side projection. Ordered by
/// <c>CreatedAt DESC</c> so the most-recently-created suggestions appear first.</para>
/// </summary>
public sealed class ListKGSuggestionsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<ListKGSuggestionsQuery, PaginatedResult<KGSuggestionDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public ListKGSuggestionsQueryHandler(
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

    public async Task<PaginatedResult<KGSuggestionDto>> Handle(
        ListKGSuggestionsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = _db.KGSuggestions.AsNoTracking();

            // Status filter — default to Pending only when not specified.
            if (request.Status.HasValue)
                query = query.Where(s => s.Status == request.Status.Value);
            else
                query = query.Where(s => s.Status == KGSuggestionStatus.Pending);

            // Note: SubjectCode / GradeId filters require a join to the Learning module's
            // KnowledgeNode table which is in a separate schema/DbContext. KGSuggestion only stores
            // plain int NodeIds (no cross-module FK). These filters are accepted on the query but
            // not applied here (cross-module join is out of scope for this DbContext).
            // The api-tester / future extension can apply them via a cross-module read seam.

            var ordered = query.OrderByDescending(s => s.CreatedAt);

            var pageNumber = request.PageNumber <= 0 ? 1 : request.PageNumber;
            var pageSize   = request.PageSize  <= 0 ? 20 : Math.Min(request.PageSize, 100);

            var totalCount = await ordered.CountAsync(cancellationToken);

            if (totalCount == 0)
                return PaginatedResult<KGSuggestionDto>.EmptyCollection(
                    _localizer[SharedResourcesKey.KGSuggestionsRetrievedSuccessfully]);

            var items = await ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ProjectTo<KGSuggestionDto>(_mapper.ConfigurationProvider)
                .ToListAsync(cancellationToken);

            return PaginatedResult<KGSuggestionDto>.Success(
                items, totalCount, pageNumber, pageSize,
                _localizer[SharedResourcesKey.KGSuggestionsRetrievedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in ListKGSuggestionsQuery");
            return PaginatedResult<KGSuggestionDto>.ServerError(
                _localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
