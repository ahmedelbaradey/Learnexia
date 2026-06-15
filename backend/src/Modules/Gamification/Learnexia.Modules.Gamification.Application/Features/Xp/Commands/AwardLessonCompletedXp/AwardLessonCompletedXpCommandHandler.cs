using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardLessonCompletedXp;

/// <summary>
/// Handles <see cref="AwardLessonCompletedXpCommand"/>.
/// Delegates all workflow logic (idempotency, practice-mode gate, dual-award XP math, profile upsert)
/// to <see cref="IXpService"/>. Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public class AwardLessonCompletedXpCommandHandler
    : BaseResponseHandler, ICommandHandler<AwardLessonCompletedXpCommand, BaseResponse<Unit>>
{
    private readonly IXpService _xpService;
    private readonly ILoggerManager _logger;

    public AwardLessonCompletedXpCommandHandler(
        IXpService xpService,
        ILoggerManager logger)
    {
        _xpService = xpService;
        _logger    = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        AwardLessonCompletedXpCommand request,
        CancellationToken cancellationToken)
    {
        return await _xpService.AwardLessonCompletedXpAsync(request, cancellationToken);
    }
}
