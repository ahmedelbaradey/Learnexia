using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.RegainHeartFromPractice;

/// <summary>
/// Handles <see cref="RegainHeartFromPracticeCommand"/>.
/// Delegates all workflow logic (row-lock, lazy-refill, practice-mode guard, heart regen)
/// to <see cref="IHeartService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public class RegainHeartFromPracticeCommandHandler
    : BaseResponseHandler, ICommandHandler<RegainHeartFromPracticeCommand, BaseResponse<Unit>>
{
    private readonly IHeartService _heartService;
    private readonly ILoggerManager _logger;

    public RegainHeartFromPracticeCommandHandler(
        IHeartService heartService,
        ILoggerManager logger)
    {
        _heartService = heartService;
        _logger       = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        RegainHeartFromPracticeCommand request,
        CancellationToken cancellationToken)
    {
        return await _heartService.RegainHeartFromPracticeAsync(request, cancellationToken);
    }
}
