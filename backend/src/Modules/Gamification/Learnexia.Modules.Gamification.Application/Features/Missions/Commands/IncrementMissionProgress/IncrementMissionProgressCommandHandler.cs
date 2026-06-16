using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;

/// <summary>
/// Handles <see cref="IncrementMissionProgressCommand"/> (P4-06, FR-GM-5, AC2 + AC3).
/// Delegates all workflow logic (cheap probe, row-lock, idempotency, attach convention,
/// progress log staging, inline completion) to <see cref="IMissionService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class IncrementMissionProgressCommandHandler
    : BaseResponseHandler, ICommandHandler<IncrementMissionProgressCommand, BaseResponse<Unit>>
{
    private readonly IMissionService _missionService;
    private readonly ILoggerManager _logger;

    public IncrementMissionProgressCommandHandler(
        IMissionService missionService,
        ILoggerManager logger)
    {
        _missionService = missionService;
        _logger         = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        IncrementMissionProgressCommand request,
        CancellationToken ct)
    {
        return await _missionService.IncrementMissionProgressAsync(request, ct);
    }
}
