using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Application.Features.AuditLog.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Moderation.Application.Features.AuditLog.Queries.GetAuditLog;

/// <summary>
/// Handles <see cref="GetAuditLogQuery"/>. Thin handler — clamps page inputs, then delegates
/// all EF/query/pagination/UTC-normalization logic to <see cref="IAuditLogQueryService"/>
/// (Option C: no DbContext or EF types in this handler).
///
/// Results are ordered newest-first. Queries are NOT auto-validated; filter inputs are
/// validated in this handler.
/// </summary>
public class GetAuditLogQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAuditLogQuery, BaseResponse<PaginatedResult<AuditLogDto>>>
{
    private readonly IAuditLogQueryService _queryService;
    private readonly ILoggerManager _logger;

    public GetAuditLogQueryHandler(
        IAuditLogQueryService queryService,
        ILoggerManager logger)
    {
        _queryService = queryService;
        _logger = logger;
    }

    public async Task<BaseResponse<PaginatedResult<AuditLogDto>>> Handle(
        GetAuditLogQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Clamp page size to prevent full-table loads (security: no unbounded queries).
            var pageSize   = Math.Clamp(request.PageSize, 1, 100);
            var pageNumber = request.PageNumber < 1 ? 1 : request.PageNumber;

            var result = await _queryService.GetPagedAsync(
                adminUserId:      request.AdminUserId,
                actionType:       request.ActionType,
                targetEntityType: request.TargetEntityType,
                dateFrom:         request.DateFrom,
                dateTo:           request.DateTo,
                pageNumber:       pageNumber,
                pageSize:         pageSize,
                cancellationToken: cancellationToken);

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
