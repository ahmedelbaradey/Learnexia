using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Queries.ListKGSuggestions;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
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
///
/// <para>When both <c>SubjectCode</c> and <c>GradeId</c> are provided, the handler resolves the
/// matching KnowledgeNode ids via <see cref="IKnowledgeNodeReader"/> (Shared.Contracts cross-module
/// seam) and filters suggestions to only those whose <c>SourceNodeId</c> or <c>TargetNodeId</c>
/// is in that node set. If neither param is supplied the filter is skipped.</para>
/// </summary>
public sealed class ListKGSuggestionsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<ListKGSuggestionsQuery, PaginatedResult<KGSuggestionDto>>
{
    private readonly CurriculumDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IKnowledgeNodeReader _nodeReader;

    public ListKGSuggestionsQueryHandler(
        CurriculumDbContext db,
        IMapper mapper,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        IKnowledgeNodeReader nodeReader)
    {
        _db         = db;
        _mapper     = mapper;
        _logger     = logger;
        _localizer  = localizer;
        _nodeReader = nodeReader;
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

            // Subject+Grade filter — only applied when BOTH params are supplied.
            // Resolves node ids via IKnowledgeNodeReader (cross-module seam; no direct project ref
            // from Curriculum to Learning). Filters suggestions whose SourceNodeId or TargetNodeId
            // belongs to those nodes. When the resolved node set is empty, no suggestions match.
            if (request.SubjectCode.HasValue && request.GradeId.HasValue)
            {
                var subjectNodes = await _nodeReader.GetNodesForSubjectAsync(
                    request.SubjectCode.Value,
                    request.GradeId.Value,
                    cancellationToken);

                var nodeIdSet = subjectNodes.Select(n => n.NodeId).ToList();

                // If no nodes exist for that subject+grade the result set will be empty — short-circuit.
                if (nodeIdSet.Count == 0)
                    return PaginatedResult<KGSuggestionDto>.EmptyCollection(
                        _localizer[SharedResourcesKey.KGSuggestionsRetrievedSuccessfully]);

                // EF Core translates List<int>.Contains as SQL IN (...) — server-side, no client eval.
                query = query.Where(s =>
                    nodeIdSet.Contains(s.SourceNodeId) ||
                    nodeIdSet.Contains(s.TargetNodeId));
            }

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
