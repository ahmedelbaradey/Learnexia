using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Preferences.Queries.GetMyPreferences;

/// <summary>
/// Loads the caller's notification preferences from the <c>notifications</c> schema (P2-12 BE-1).
/// Self-scoped: UserId is always resolved from the JWT via <see cref="ICurrentUserService"/> — never
/// from a parameter. When no rows exist yet the handler returns defaults for the 4 user-visible categories
/// (no 404, nothing persisted on read). Queries are NOT auto-validated (CONVENTIONS §4).
/// <para>
/// P4-09 added <c>DailyMissionReminder = 4</c>, <c>LapseWinBack = 5</c>, and <c>System = 6</c> to the
/// enum. Those categories are parent-controlled per-child (stored in <c>ChildReengagementPreference</c>)
/// and NOT managed via this self-preferences endpoint. Only the original 4 self-managed categories are
/// returned here.
/// </para>
/// </summary>
public sealed class GetMyNotificationPreferencesQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetMyNotificationPreferencesQuery, BaseResponse<NotificationPreferencesResponse>>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public GetMyNotificationPreferencesQueryHandler(
        INotificationsDbContext db,
        ICurrentUserService currentUserService,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _db = db;
        _currentUserService = currentUserService;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<NotificationPreferencesResponse>> Handle(
        GetMyNotificationPreferencesQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<NotificationPreferencesResponse>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            var rows = await _db.NotificationPreferences
                .AsNoTracking()
                .Where(p => p.UserId == userId.Value)
                .ToListAsync(cancellationToken);

            // Build a lookup so we can fall back to defaults for missing categories without a second DB hit.
            var stored = rows.ToDictionary(r => r.Category);

            // P4-09 guard: only the 4 original user-visible categories belong to the self-preferences
            // endpoint. Categories 4 (DailyMissionReminder), 5 (LapseWinBack), and 6 (System) are either
            // parent-controlled per-child (ChildReengagementPreference) or internal sentinels — they must
            // NOT be surfaced here. Using an explicit allow-list keeps the set stable even if the enum grows.
            var userFacingCategories = new[]
            {
                NotificationCategory.WeeklyReport,
                NotificationCategory.StreakAtRisk,
                NotificationCategory.ProductAnnouncement,
                NotificationCategory.Achievement,
            };

            var items = new List<NotificationPreferenceItemDto>(userFacingCategories.Length);

            foreach (var category in userFacingCategories)
            {
                if (stored.TryGetValue(category, out var row))
                {
                    items.Add(new NotificationPreferenceItemDto
                    {
                        Category = row.Category,
                        EmailEnabled = row.EmailEnabled,
                        PushEnabled = row.PushEnabled,
                    });
                }
                else
                {
                    // Default values: WeeklyReport gets email on; all others default off.
                    // Defaults are synthesised in memory — NOT persisted (no side effects on read).
                    items.Add(new NotificationPreferenceItemDto
                    {
                        Category = category,
                        EmailEnabled = category == NotificationCategory.WeeklyReport,
                        PushEnabled = false,
                    });
                }
            }

            var response = new NotificationPreferencesResponse { Preferences = items };
            return Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in GetMyNotificationPreferencesQuery");
            return ServerError<NotificationPreferencesResponse>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
