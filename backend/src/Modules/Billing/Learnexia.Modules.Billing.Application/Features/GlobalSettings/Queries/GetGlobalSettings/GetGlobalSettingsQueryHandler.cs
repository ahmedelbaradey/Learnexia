using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Application.Features.GlobalSettings.Dtos;
using Learnexia.Modules.Billing.Domain.Constants;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Billing.Application.Features.GlobalSettings.Queries.GetGlobalSettings;

/// <summary>
/// Returns all managed <c>GlobalSetting</c> rows from <c>platform.GlobalSettings</c>.
/// Admin-only; bound by <c>[Authorize(AdminPolicy)]</c> on the controller.
/// </summary>
public sealed class GetGlobalSettingsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetGlobalSettingsQuery, BaseResponse<IReadOnlyList<GlobalSettingDto>>>
{
    private readonly IBillingDbContext _db;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetGlobalSettingsQueryHandler(
        IBillingDbContext db,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _db       = db;
        _logger   = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<IReadOnlyList<GlobalSettingDto>>> Handle(
        GetGlobalSettingsQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            // Materialize ManagedKeys to a List<string> first so EF Core can translate
            // the Contains call — IReadOnlySet<string> is not translatable by EF Core.
            var managedKeys = GlobalSettingKeys.ManagedKeys.ToList();

            var rows = await _db.GlobalSettings
                .AsNoTracking()
                .Where(s => managedKeys.Contains(s.Key))
                .OrderBy(s => s.Key)
                .Select(s => new GlobalSettingDto
                {
                    Key       = s.Key,
                    Value     = s.Value,
                    Type      = s.Type,
                    UpdatedBy = s.UpdatedBy,
                    UpdatedAt = s.UpdatedAt,
                })
                .ToListAsync(cancellationToken);

            return Success<IReadOnlyList<GlobalSettingDto>>(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGlobalSettingsQueryHandler: failed to retrieve global settings.");
            return ServerError<IReadOnlyList<GlobalSettingDto>>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
