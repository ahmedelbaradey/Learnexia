using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.ExpireTimedEvent;

/// <summary>Admin command: manually expire (deactivate) a <c>TimedEvent</c>.</summary>
public sealed record ExpireTimedEventCommand(int Id) : ICommand<BaseResponse<bool>>;
