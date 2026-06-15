using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.LoseHeart;

/// <summary>
/// Handles <see cref="LoseHeartCommand"/>.
/// Delegates all workflow logic (stale-event guard, idempotency, lazy-refill, heart decrement,
/// audit row staging) to <see cref="IHeartService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public class LoseHeartCommandHandler
    : BaseResponseHandler, ICommandHandler<LoseHeartCommand, BaseResponse<Unit>>
{
    private readonly IHeartService _heartService;
    private readonly ILoggerManager _logger;

    public LoseHeartCommandHandler(
        IHeartService heartService,
        ILoggerManager logger)
    {
        _heartService = heartService;
        _logger       = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        LoseHeartCommand request,
        CancellationToken cancellationToken)
    {
        return await _heartService.LoseHeartAsync(request, cancellationToken);
    }
}
