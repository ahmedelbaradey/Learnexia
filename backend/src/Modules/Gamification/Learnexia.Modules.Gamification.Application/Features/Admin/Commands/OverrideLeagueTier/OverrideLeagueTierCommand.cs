using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.OverrideLeagueTier;

/// <summary>
/// Admin command: override the league tier for a student.
/// Sets <c>StudentXpProfile.CurrentTier</c> and stamps the active-week
/// <c>LeagueMembership</c> record to match. Requires <c>Confirm = true</c>.
/// </summary>
public sealed record OverrideLeagueTierCommand(
    int ChildId,
    LeagueTier NewTier,
    string Reason,
    bool Confirm) : ICommand<BaseResponse<bool>>;
