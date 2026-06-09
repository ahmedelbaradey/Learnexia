using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateTimedEvent;

/// <summary>Admin command: create a new <c>TimedEvent</c> catalog row.</summary>
public sealed record CreateTimedEventCommand(
    string Code,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    DateTime StartUtc,
    DateTime EndUtc,
    decimal Multiplier,
    TimedEventScope Scope) : ICommand<BaseResponse<int>>;
