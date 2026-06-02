using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.UpdateChildReengagementPreferences;

/// <summary>
/// Upserts the parent's per-child re-engagement preferences (P4-09 B4-3 / AC3).
/// Writes all 3 schedulable categories in an explicit transaction (no Unit of Work, ADR 0001).
/// Anti-IDOR / anti-enumeration: generic Forbidden regardless of whether the child exists or the
/// link doesn't — same message as the read endpoint.
/// </summary>
public sealed class UpdateChildReengagementPreferencesCommandHandler
    : BaseResponseHandler,
      ICommandHandler<UpdateChildReengagementPreferencesCommand, BaseResponse<string>>
{
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

    public UpdateChildReengagementPreferencesCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(
        UpdateChildReengagementPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var parentId = _currentUserService.UserId;
            if (parentId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Anti-IDOR: same generic Forbidden regardless of child-exists / link-exists.
            var isParent = await _parentChildQuery.IsParentOfChildAsync(parentId.Value, request.ChildId, cancellationToken);
            if (!isParent)
                return Forbidden<string>(_localizer[SharedResourcesKey.NotAuthorizedForChild]);

            // Load existing rows for this (parentId, childId) so we can upsert.
            var existing = await _db.ChildReengagementPreferences
                .Where(p => p.ParentId == parentId.Value && p.ChildId == request.ChildId)
                .ToDictionaryAsync(p => p.Category, cancellationToken);

            // Build a lookup for the incoming items.
            var incoming = (request.Items ?? [])
                .ToDictionary(i => i.Category);

            // Explicit transaction: all rows must commit atomically (ADR 0001).
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var category in ReengagementCategories)
            {
                if (existing.TryGetValue(category, out var row))
                {
                    // Update existing row.
                    if (incoming.TryGetValue(category, out var item))
                        row.UpdateChannels(item.Email, item.Push, item.InApp);

                    if (request.QuietHoursStartLocal.HasValue && request.QuietHoursEndLocal.HasValue)
                        row.UpdateQuietHours(request.QuietHoursStartLocal.Value, request.QuietHoursEndLocal.Value,
                            request.TimeZoneId ?? row.TimeZoneId);

                    if (request.DailyCap.HasValue)
                        row.UpdateDailyCap(request.DailyCap.Value);
                }
                else
                {
                    // Create a default row, then apply the incoming overrides.
                    var newRow = ChildReengagementPreference.CreateDefault(parentId.Value, request.ChildId, category);

                    if (incoming.TryGetValue(category, out var item))
                        newRow.UpdateChannels(item.Email, item.Push, item.InApp);

                    if (request.QuietHoursStartLocal.HasValue && request.QuietHoursEndLocal.HasValue)
                        newRow.UpdateQuietHours(request.QuietHoursStartLocal.Value, request.QuietHoursEndLocal.Value,
                            request.TimeZoneId ?? newRow.TimeZoneId);

                    if (request.DailyCap.HasValue)
                        newRow.UpdateDailyCap(request.DailyCap.Value);

                    await _db.ChildReengagementPreferences.AddAsync(newRow, cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.ChildPreferencesUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateChildReengagementPreferencesCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
