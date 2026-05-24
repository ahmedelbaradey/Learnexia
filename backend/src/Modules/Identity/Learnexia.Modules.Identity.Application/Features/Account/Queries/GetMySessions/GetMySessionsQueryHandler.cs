using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Queries.GetMySessions;

/// <summary>
/// Returns the caller's own sessions via <see cref="ISessionManagementService"/> (P2-12 SEC-2).
/// Self-scoped: UserId from JWT only. No admin-bypass surface on this endpoint.
/// </summary>
public sealed class GetMySessionsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetMySessionsQuery, BaseResponse<List<SessionInfo>>>
{
    private readonly ISessionManagementService _sessionManagementService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public GetMySessionsQueryHandler(
        ISessionManagementService sessionManagementService,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _sessionManagementService = sessionManagementService;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<List<SessionInfo>>> Handle(GetMySessionsQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<List<SessionInfo>>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var sessions = await _sessionManagementService.GetUserSessionsAsync(userId.Value);

            // Project UserSession → SessionInfo (the publicly-safe session descriptor).
            var sessionInfos = sessions
                .Select(s => new SessionInfo
                {
                    SessionId = s.SessionId,
                    IsActive = s.IsActive,
                    IsExpired = s.IsExpired,
                    ExpiresAt = s.ExpiresAt,
                    RemainingSeconds = (int)Math.Max(0, s.RemainingTime.TotalSeconds),
                    LastActivityAt = s.LastActivityAt,
                    CreatedAt = s.CreatedAt,
                })
                .ToList();

            return Success(sessionInfos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetMySessionsQuery");
            return ServerError<List<SessionInfo>>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
