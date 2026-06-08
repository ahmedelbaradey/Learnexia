using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserProfile;

public class GetAdminUserProfileQueryHandler : BaseResponseHandler, IQueryHandler<GetAdminUserProfileQuery, BaseResponse<AdminUserProfileDto>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IPublisher _publisher;

    public GetAdminUserProfileQueryHandler(
        IIdentityServiceManager service,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer,
        ICurrentUserService currentUserService,
        IPublisher publisher)
    {
        _service = service;
        _logger = logger;
        _localizer = localizer;
        _currentUserService = currentUserService;
        _publisher = publisher;
    }

    public async Task<BaseResponse<AdminUserProfileDto>> Handle(GetAdminUserProfileQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // ── Input guard (query — not auto-validated) ──
            if (request.UserId <= 0)
                return BadRequest<AdminUserProfileDto>(_localizer[SharedResourcesKey.UserNotFound]);

            var user = await _service.UserManagmentService.FindByIdAsync(request.UserId.ToString());
            if (user == null)
                return NotFound<AdminUserProfileDto>(_localizer[SharedResourcesKey.UserNotFound]);

            var roles = await _service.UserManagmentService.GetUserRolesAsync(user);
            var roleList = roles.ToList();

            var dto = new AdminUserProfileDto
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Role = roleList.FirstOrDefault(),
                Roles = roleList,
                AccountStatus = user.AccountStatus,
                IsActive = user.IsActive,
                LastStatusReason = user.LastStatusReason,
                StatusChangedAtUtc = user.StatusChangedAtUtc,
                CreatedAt = user.CreatedAt,
                PreferredLanguage = user.PreferredLanguage,
                LearningLanguage = user.LearningLanguage,
                Grade = user.Grade,
                Nationality = user.Nationality,
                AvatarUrl = user.AvatarUrl,
            };

            // ── Audit emit: post-read, best-effort ──
            var adminUserId = _currentUserService.UserId ?? 0;
            _ = PublishAuditEventAsync(adminUserId, AdminActions.UserViewed, user.Id, cancellationToken);

            _logger.LogInfo($"Admin profile read for userId={user.Id}");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.AdminUserProfileRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetAdminUserProfileQueryHandler");
            return ServerError<AdminUserProfileDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    private async Task PublishAuditEventAsync(int adminUserId, string action, int targetId, CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(
                new AdminActionPerformedEvent(
                    Guid.NewGuid(),
                    DateTime.UtcNow,
                    adminUserId,
                    action,
                    "User",
                    targetId,
                    null),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish AdminActionPerformedEvent (best-effort, ignored)");
        }
    }
}
