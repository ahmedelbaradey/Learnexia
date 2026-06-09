using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateTimedEvent;

/// <summary>Admin command: update mutable fields on an existing <c>TimedEvent</c>. Code and IsActive are not changed here.</summary>
public sealed record UpdateTimedEventCommand(
    int Id,
    string NameEn,
    string NameAr,
    string? DescriptionEn,
    string? DescriptionAr,
    DateTime StartUtc,
    DateTime EndUtc,
    decimal Multiplier,
    TimedEventScope Scope) : ICommand<BaseResponse<bool>>;
