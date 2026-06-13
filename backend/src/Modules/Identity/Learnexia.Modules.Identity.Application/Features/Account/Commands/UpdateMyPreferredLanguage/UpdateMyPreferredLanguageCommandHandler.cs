using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.UpdateMyPreferredLanguage;

// Mirrors UpdateMyProfileCommandHandler: caller resolved from the JWT (never the body),
// no Unit of Work (UserManager.UpdateAsync commits the single row), localized messages.
// Security: only the single PreferredLanguage field is written — no mass-assignment surface.
public class UpdateMyPreferredLanguageCommandHandler : BaseResponseHandler, ICommandHandler<UpdateMyPreferredLanguageCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _service;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UpdateMyPreferredLanguageCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(UpdateMyPreferredLanguageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Self-scope (fail-closed): the edited user is ALWAYS the authenticated caller,
            // resolved from the JWT — never an id from the request body. No id means no
            // token → Unauthorized. This prevents IDOR by design.
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var user = await _service.UserManagmentService.FindByIdAsync(userId.Value.ToString());
            if (user is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Only the single PreferredLanguage field is touched — mirrors
            // EditUserPreferredLanguageCommandHandler's field assignment exactly.
            user.PreferredLanguage = request.UserPreferredLanguage;

            // No Unit of Work — UpdateAsync commits the single row immediately.
            var result = await _service.UserManagmentService.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                _logger.LogError(null, $"UpdateMyPreferredLanguage UpdateAsync failed for user {userId}: {errors}");
                return BadRequest<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
            }

            _logger.LogInfo($"Updated preferred language for user {userId}.");
            return Success<string>(_localizer[SharedResourcesKey.PreferredLanguageUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateMyPreferredLanguageCommand");
            return ServerError<string>(ex.Message);
        }
    }
}
