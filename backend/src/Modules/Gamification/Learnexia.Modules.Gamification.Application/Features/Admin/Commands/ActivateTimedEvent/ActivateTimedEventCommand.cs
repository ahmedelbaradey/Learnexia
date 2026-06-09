using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.ActivateTimedEvent;

/// <summary>Admin command: manually activate a <c>TimedEvent</c> (sets IsActive = true).</summary>
public sealed record ActivateTimedEventCommand(int Id) : ICommand<BaseResponse<bool>>;
