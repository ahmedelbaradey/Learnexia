using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Application.Features.AuditLog.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Moderation.Application.Features.AuditLog.Queries.GetAuditLog;

/// <summary>
/// Handles <see cref="GetAuditLogQuery"/>. Applies all filters DB-side (no in-memory load).
/// Results are ordered newest-first. Queries are NOT auto-validated; filter inputs are
/// validated in this handler.
/// </summary>
public class GetAuditLogQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAuditLogQuery, BaseResponse<PaginatedResult<AuditLogDto>>>
{
    private readonly IModerationDbContext _db;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;

    public GetAuditLogQueryHandler(
        IModerationDbContext db,
        IMapper mapper,
        ILoggerManager logger)
    {
        _db = db;
        _mapper = mapper;
        _logger = logger;
    }

    public async Task<BaseResponse<PaginatedResult<AuditLogDto>>> Handle(
        GetAuditLogQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Clamp page size to prevent full-table loads (security lesson: no unbounded queries).
            var pageSize = Math.Clamp(request.PageSize, 1, 100);
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            // Build DB-side filter — all predicates applied before materialisation.
            var query = _db.AuditLogs.AsQueryable();

            if (request.AdminUserId.HasValue)
                query = query.Where(a => a.AdminUserId == request.AdminUserId.Value);

            if (!string.IsNullOrWhiteSpace(request.ActionType))
                query = query.Where(a => a.Action == request.ActionType);

            if (!string.IsNullOrWhiteSpace(request.TargetEntityType))
                query = query.Where(a => a.TargetEntityType == request.TargetEntityType);

            if (request.DateFrom.HasValue)
                query = query.Where(a => a.OccurredAtUtc >= request.DateFrom.Value);

            if (request.DateTo.HasValue)
                query = query.Where(a => a.OccurredAtUtc <= request.DateTo.Value);

            // Default ordering: newest action first.
            query = query.OrderByDescending(a => a.OccurredAtUtc);

            var result = await _mapper.ProjectTo<AuditLogDto>(query)
                .ToPaginatedListAsync(pageNumber, pageSize, orderBy: null);

            if (result.TotalCount == 0)
                return EmptyCollection(PaginatedResult<AuditLogDto>.Success(
                    new List<AuditLogDto>(), 0, pageNumber, pageSize));

            return Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetAuditLogQuery");
            return ServerError<PaginatedResult<AuditLogDto>>();
        }
    }
}
