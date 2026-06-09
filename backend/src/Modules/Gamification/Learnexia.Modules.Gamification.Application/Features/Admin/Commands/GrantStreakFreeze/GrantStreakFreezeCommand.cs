using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.GrantStreakFreeze;

/// <summary>
/// Admin command: grant up to <see cref="Count"/> streak freezes to a student, clamped at
/// <c>StudentXpProfile.MaxFreezes</c>. Requires <c>Confirm = true</c>.
/// </summary>
public sealed record GrantStreakFreezeCommand(
    int ChildId,
    int Count,
    string Reason,
    bool Confirm) : ICommand<BaseResponse<bool>>;
