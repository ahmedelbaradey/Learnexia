using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Streaks.Commands.AdvanceStreak;

/// <summary>
/// Handles <see cref="AdvanceStreakCommand"/>.
/// Delegates all workflow logic (idempotency, practice-mode gate, day classification,
/// freeze milestone, StreakBonus XP staging) to <see cref="IStreakService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public class AdvanceStreakCommandHandler
    : BaseResponseHandler, ICommandHandler<AdvanceStreakCommand, BaseResponse<Unit>>
{
    private readonly IStreakService _streakService;
    private readonly ILoggerManager _logger;

    public AdvanceStreakCommandHandler(
        IStreakService streakService,
        ILoggerManager logger)
    {
        _streakService = streakService;
        _logger        = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        AdvanceStreakCommand request,
        CancellationToken cancellationToken)
    {
        return await _streakService.AdvanceStreakAsync(request, cancellationToken);
    }
}
