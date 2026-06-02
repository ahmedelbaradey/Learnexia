using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Queries.GetChildReengagementPreferences;

/// <summary>
/// Loads (and synthesises defaults for) the parent's per-child re-engagement preferences (P4-09 B4-3 / AC3).
///
/// The 3 schedulable categories are <see cref="NotificationCategory.StreakAtRisk"/>,
/// <see cref="NotificationCategory.DailyMissionReminder"/>, and
/// <see cref="NotificationCategory.LapseWinBack"/>. For any category without a persisted row a
/// default-valued DTO is returned in-memory (nothing is written on read — mirrors
/// <c>GetMyNotificationPreferencesQueryHandler</c>).
///
/// Anti-IDOR / anti-enumeration: any failure of the parent-child link check returns the same
/// generic Forbidden message regardless of whether the child exists.
/// </summary>
public sealed class GetChildReengagementPreferencesQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetChildReengagementPreferencesQuery, BaseResponse<List<ChildReengagementPreferenceDto>>>
{
    // The 3 categories that are parent-controlled per-child (AC1).
    private static readonly NotificationCategory[] ReengagementCategories =
    [
        NotificationCategory.StreakAtRisk,
        NotificationCategory.DailyMissionReminder,
        NotificationCategory.LapseWinBack,
    ];

    private readonly INotificationsDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public GetChildReengagementPreferencesQueryHandler(
        INotificationsDbContext db,
        ICurrentUserService currentUserService,
        IParentChildQuery parentChildQuery,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db                 = db;
        _currentUserService = currentUserService;
        _parentChildQuery   = parentChildQuery;
        _localizer          = localizer;
        _logger             = logger;
    }

    public async Task<BaseResponse<List<ChildReengagementPreferenceDto>>> Handle(
        GetChildReengagementPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<List<ChildReengagementPreferenceDto>>(
                    _localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Anti-IDOR: same generic Forbidden regardless of child-exists / link-exists (AC3).
            var isParent = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isParent)
                return Forbidden<List<ChildReengagementPreferenceDto>>(
                    _localizer[SharedResourcesKey.NotAuthorizedForChild]);

            // Load all persisted rows for this (parentId, childId).
            var stored = await _db.ChildReengagementPreferences
                .AsNoTracking()
                .Where(p => p.ParentId == parentId.Value && p.ChildId == request.ChildId)
                .ToDictionaryAsync(p => p.Category, cancellationToken);

            // Synthesise one DTO per schedulable category; use defaults where no row exists.
            var result = new List<ChildReengagementPreferenceDto>(ReengagementCategories.Length);

            foreach (var category in ReengagementCategories)
            {
                if (stored.TryGetValue(category, out var row))
                {
                    result.Add(ToDto(row));
                }
                else
                {
                    // Conservative defaults (AC3): Push=false, InApp=true, DailyCap=3.
                    result.Add(BuildDefault(category));
                }
            }

            return Success(result, _localizer[SharedResourcesKey.ChildPreferencesRetrievedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetChildReengagementPreferencesQuery");
            return ServerError<List<ChildReengagementPreferenceDto>>(
                _localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ChildReengagementPreferenceDto ToDto(ChildReengagementPreference row)
        => new()
        {
            Category            = row.Category,
            Email               = row.Email,
            Push                = row.Push,
            InApp               = row.InApp,
            QuietHoursStartLocal = row.QuietHoursStartLocal.ToString("HH:mm"),
            QuietHoursEndLocal  = row.QuietHoursEndLocal.ToString("HH:mm"),
            TimeZoneId          = row.TimeZoneId,
            DailyCap            = row.DailyCap,
        };

    private static ChildReengagementPreferenceDto BuildDefault(NotificationCategory category)
        => new()
        {
            Category            = category,
            Email               = false,
            Push                = false,   // Conservative default — parent must opt-in (AC3).
            InApp               = true,
            QuietHoursStartLocal = "22:00",
            QuietHoursEndLocal  = "08:00",
            TimeZoneId          = "Africa/Cairo",
            DailyCap            = 3,
        };

    // Override Success to accept a localised message (mirrors existing handler pattern).
    private BaseResponse<T> Success<T>(T entity, string message) => new()
    {
        StatusCode = System.Net.HttpStatusCode.OK,
        Successed  = true,
        Data       = entity,
        Message    = message,
    };
}
