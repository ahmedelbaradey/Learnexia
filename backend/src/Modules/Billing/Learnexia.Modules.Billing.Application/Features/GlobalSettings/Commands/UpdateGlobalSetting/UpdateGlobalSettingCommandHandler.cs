using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.GlobalSettings.Commands.UpdateGlobalSetting;

/// <summary>
/// Thin handler (Option C): delegates all EF / transaction / post-commit event staging /
/// cache-invalidation logic to <see cref="IGlobalSettingService.UpdateAsync"/>. Stays EF-free.
///
/// <para><c>UpdatedBy</c> is derived server-side from JWT claims via <c>ICurrentUserService</c>
/// (prevents request-body spoofing).</para>
/// </summary>
public sealed class UpdateGlobalSettingCommandHandler
    : BaseResponseHandler,
      ICommandHandler<UpdateGlobalSettingCommand, BaseResponse<bool>>
{
    private readonly IGlobalSettingService _globalSettingService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UpdateGlobalSettingCommandHandler(
        IGlobalSettingService globalSettingService,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _globalSettingService = globalSettingService;
        _currentUser          = currentUser;
        _logger               = logger;
        _localizer            = localizer;
    }

    public async Task<BaseResponse<bool>> Handle(
        UpdateGlobalSettingCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Derive UpdatedBy server-side from JWT — prevents request-body spoofing.
            var adminUserId = _currentUser.UserId ?? 0;
            var updatedBy   = _currentUser.UserName ?? adminUserId.ToString();

            var result = await _globalSettingService.UpdateAsync(
                key         : request.Key,
                newValue    : request.NewValue,
                adminUserId : adminUserId,
                updatedBy   : updatedBy,
                ct          : cancellationToken);

            if (result.FailureReason == GlobalSettingUpdateFailureReason.NotFound)
                return NotFound<bool>(_localizer[SharedResourcesKey.GlobalSettingNotFound]);

            _logger.LogInfo($"UpdateGlobalSettingCommandHandler: updated key={request.Key} by={updatedBy}.");
            return Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"UpdateGlobalSettingCommandHandler: failed for key={request.Key}.");
            return ServerError<bool>(_localizer[SharedResourcesKey.AnErrorIsOccurredWhileSavingData]);
        }
    }
}
