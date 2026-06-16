using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;

/// <summary>
/// Handles <see cref="AwardBadgeCommand"/>.
/// Delegates all workflow logic (badge catalog load, profile load/upsert, idempotency,
/// XP boost, domain mutation, StudentBadge + XpAward row staging)
/// to <see cref="IBadgeService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class AwardBadgeCommandHandler
    : BaseResponseHandler, ICommandHandler<AwardBadgeCommand, BaseResponse<Unit>>
{
    private readonly IBadgeService _badgeService;
    private readonly ILoggerManager _logger;

    public AwardBadgeCommandHandler(
        IBadgeService badgeService,
        ILoggerManager logger)
    {
        _badgeService = badgeService;
        _logger       = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(AwardBadgeCommand request, CancellationToken ct)
    {
        return await _badgeService.AwardBadgeAsync(request, ct);
    }
}
