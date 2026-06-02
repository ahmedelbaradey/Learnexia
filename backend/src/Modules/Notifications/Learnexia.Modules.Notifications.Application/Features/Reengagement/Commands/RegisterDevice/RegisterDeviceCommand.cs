using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RegisterDevice;

/// <summary>
/// Registers or re-activates an Expo push token for the authenticated user (P4-09 B4-4 / AC4).
/// Upsert semantics by <c>(UserId, ExpoPushToken)</c>: if the token already exists and is
/// revoked, it is reactivated; if it belongs to a different user, ownership is transferred.
/// </summary>
public sealed record RegisterDeviceCommand(
    string ExpoPushToken,
    string Platform) : ICommand<BaseResponse<string>>;
