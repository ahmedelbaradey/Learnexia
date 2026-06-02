using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Commands.RevokeDevice;

/// <summary>
/// Deactivates a device push token by its integer ID for the authenticated user (P4-09 B4-4 / F-01).
/// Using the integer PK instead of the raw push-token string prevents the token from leaking in
/// reverse-proxy logs, CDN logs, or browser history (security fix F-01).
/// Anti-IDOR: only deactivates a token that belongs to the current user.
/// Idempotent: not-found or already-deactivated tokens return 404 (anti-enumeration).
/// </summary>
public sealed record RevokeDeviceCommand(int TokenId) : ICommand<BaseResponse<string>>;
