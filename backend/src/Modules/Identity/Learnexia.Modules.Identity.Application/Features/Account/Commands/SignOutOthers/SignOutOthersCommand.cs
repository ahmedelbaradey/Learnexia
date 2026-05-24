using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Account.Commands.SignOutOthers;

/// <summary>
/// Terminates all sessions for the authenticated caller except the current one (P2-12 SEC-2). Does NOT
/// revoke the refresh token (the caller remains logged in). Self-scoped: UserId from JWT only.
/// </summary>
public sealed record SignOutOthersCommand : ICommand<BaseResponse<string>>;
