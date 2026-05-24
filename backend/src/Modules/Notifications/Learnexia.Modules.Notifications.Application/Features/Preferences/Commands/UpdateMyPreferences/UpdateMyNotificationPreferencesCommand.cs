using Learnexia.Modules.Notifications.Application.Features.Preferences.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Preferences.Commands.UpdateMyPreferences;

/// <summary>
/// Upserts the authenticated caller's notification preferences (P2-12 BE-1). Receives the full set of
/// 4 categories; each is either inserted or updated atomically inside an explicit transaction (no UoW,
/// ADR 0001). Self-scoped: UserId is always resolved from the JWT inside the handler
/// (<see cref="Shared.Kernel.Abstractions.ICurrentUserService"/>), never from the request body.
/// </summary>
public sealed record UpdateMyNotificationPreferencesCommand : ICommand<BaseResponse<string>>
{
    /// <summary>The complete list of category preferences to apply (replaces existing rows).</summary>
    public List<NotificationPreferenceItemDto> Preferences { get; init; } = new();
}
