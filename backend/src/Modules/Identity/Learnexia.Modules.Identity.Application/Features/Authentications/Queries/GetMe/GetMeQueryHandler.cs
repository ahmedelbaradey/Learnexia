using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Queries.GetMe;

public class GetMeQueryHandler : BaseResponseHandler, IQueryHandler<GetMeQuery, BaseResponse<MeResponse>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public GetMeQueryHandler(
        IIdentityServiceManager service,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _service = service;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<MeResponse>> Handle(GetMeQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Self-scope (fail-closed): the user is keyed on the authenticated caller's id, resolved
            // from the JWT — never a body/query param. No id means no token → Unauthorized.
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<MeResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var user = await _service.UserManagmentService.FindByIdAsync(userId.Value.ToString());
            if (user is null)
                return Unauthorized<MeResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Roles come from Identity for the JWT-resolved user (PascalCase, returned as-is).
            var roles = await _service.UserManagmentService.GetUserRolesAsync(user);

            // HasChildren is scoped strictly to this parent's id (no IDOR; non-parents simply have no rows).
            var hasChildren = await _service.LinkParentStudentService.ParentHasAnyChildAsync(user.Id, cancellationToken);

            var response = new MeResponse
            {
                Id = user.Id,
                Roles = roles,
                FullName = user.FullName,
                PreferredLanguage = user.PreferredLanguage,
                // Mirror SignIn: first login until registration/onboarding is completed.
                IsFirstLogin = !user.RegistrationIsCompleted,
                HasChildren = hasChildren,
            };

            return Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetMeQuery");
            return ServerError<MeResponse>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
