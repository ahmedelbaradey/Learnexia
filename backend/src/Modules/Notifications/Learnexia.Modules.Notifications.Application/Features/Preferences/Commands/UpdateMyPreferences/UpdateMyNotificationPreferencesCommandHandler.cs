using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Notifications.Application.Features.Preferences.Commands.UpdateMyPreferences;

/// <summary>
/// Upserts notification preferences (P2-12 BE-1). One row per (UserId, Category); the full set of
/// categories in the request is written in a single explicit transaction (no Unit of Work, ADR 0001).
/// Self-scoped: UserId from JWT only; no body parameter is used or trusted for identity.
/// </summary>
public sealed class UpdateMyNotificationPreferencesCommandHandler
    : BaseResponseHandler, ICommandHandler<UpdateMyNotificationPreferencesCommand, BaseResponse<string>>
{
    private readonly INotificationsDbContext _db;
    private readonly ICurrentUserService _currentUserService;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public UpdateMyNotificationPreferencesCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(
        UpdateMyNotificationPreferencesCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            var userId = _currentUserService.UserId;
            if (userId is null)
                return Unauthorized<string>(_localizer[SharedResourcesKey.UnauthorizedAccess]);

            // Load existing rows for this user so we can upsert (update or insert) without deleting first.
            var existing = await _db.NotificationPreferences
                .Where(p => p.UserId == userId.Value)
                .ToDictionaryAsync(p => p.Category, cancellationToken);

            // Explicit transaction: multiple rows must commit atomically (no Unit of Work, ADR 0001).
            await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            foreach (var dto in request.Preferences)
            {
                if (existing.TryGetValue(dto.Category, out var row))
                {
                    // Row exists — update in place.
                    row.Update(dto.EmailEnabled, dto.PushEnabled);
                }
                else
                {
                    // New row — create and add to the context.
                    var newRow = NotificationPreference.Create(userId.Value, dto.Category, dto.EmailEnabled, dto.PushEnabled);
                    await _db.NotificationPreferences.AddAsync(newRow, cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);

            return Success<string>(_localizer[SharedResourcesKey.NotificationPreferencesUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error: in UpdateMyNotificationPreferencesCommand");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
