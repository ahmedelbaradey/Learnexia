using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Leagues.Commands.IncrementLeagueXp;

/// <summary>
/// Handles <see cref="IncrementLeagueXpCommand"/> (P4-07, AC1 — weekly XP tracking).
/// Delegates all workflow logic (period key, profile resolution, membership load,
/// idempotency, LeagueXpDeltaLog staging, Redis mirror) to <see cref="ILeagueService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class IncrementLeagueXpCommandHandler
    : BaseResponseHandler, ICommandHandler<IncrementLeagueXpCommand, BaseResponse<Unit>>
{
    private readonly ILeagueService _leagueService;
    private readonly ILoggerManager _logger;

    public IncrementLeagueXpCommandHandler(
        ILeagueService leagueService,
        ILoggerManager logger)
    {
        _leagueService = leagueService;
        _logger        = logger;
    }

    public async Task<BaseResponse<Unit>> Handle(
        IncrementLeagueXpCommand request,
        CancellationToken ct)
    {
        return await _leagueService.IncrementLeagueXpAsync(request, ct);
    }
}
