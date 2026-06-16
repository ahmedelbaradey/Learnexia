using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.GlobalSettings.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.GlobalSettings.Queries.GetGlobalSettings;

/// <summary>
/// Thin handler (Option C): delegates all EF / projection logic to
/// <see cref="IGlobalSettingService.GetAllManagedAsync"/>. Stays EF-free.
/// Admin-only; bound by <c>[Authorize(AdminPolicy)]</c> on the controller.
/// </summary>
public sealed class GetGlobalSettingsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetGlobalSettingsQuery, BaseResponse<IReadOnlyList<GlobalSettingDto>>>
{
    private readonly IGlobalSettingService _globalSettingService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetGlobalSettingsQueryHandler(
        IGlobalSettingService globalSettingService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _globalSettingService = globalSettingService;
        _logger               = logger;
        _localizer            = localizer;
    }

    public async Task<BaseResponse<IReadOnlyList<GlobalSettingDto>>> Handle(
        GetGlobalSettingsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var rows = await _globalSettingService.GetAllManagedAsync(cancellationToken);
            return Success<IReadOnlyList<GlobalSettingDto>>(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGlobalSettingsQueryHandler: failed to retrieve global settings.");
            return ServerError<IReadOnlyList<GlobalSettingDto>>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
