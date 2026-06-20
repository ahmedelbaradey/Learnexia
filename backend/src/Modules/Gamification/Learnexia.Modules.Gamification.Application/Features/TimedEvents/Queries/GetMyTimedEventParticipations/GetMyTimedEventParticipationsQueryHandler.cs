using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Gamification.Application.Features.TimedEvents.Queries.GetMyTimedEventParticipations;

/// <summary>
/// Handles <see cref="GetMyTimedEventParticipationsQuery"/> — returns the active timed-event
/// participation snapshots for the JWT-resolved student (P4-12, BE-8).
///
/// Resolves <c>StudentId</c> from <c>ICurrentUserService.UserId</c> — NEVER from a client-supplied
/// parameter (IDOR-proof by construction). Delegates read logic to
/// <see cref="ITimedEventParticipationService"/>.
/// Handler is EF-free and repository-free per §7 CONVENTIONS.
/// Mirrors <see cref="GetMyMissionsQueryHandler"/> shape.
/// </summary>
public sealed class GetMyTimedEventParticipationsQueryHandler
    : BaseResponseHandler,
      IQueryHandler<GetMyTimedEventParticipationsQuery,
                    BaseResponse<IReadOnlyList<TimedEventParticipationSnapshot>>>
{
    private readonly ITimedEventParticipationService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetMyTimedEventParticipationsQueryHandler(
        ITimedEventParticipationService service,
        ICurrentUserService currentUser,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service     = service;
        _currentUser = currentUser;
        _logger      = logger;
        _localizer   = localizer;
    }

    public async Task<BaseResponse<IReadOnlyList<TimedEventParticipationSnapshot>>> Handle(
        GetMyTimedEventParticipationsQuery request, CancellationToken ct)
    {
        if (_currentUser.UserId is not { } studentId)
            return Unauthorized<IReadOnlyList<TimedEventParticipationSnapshot>>(
                _localizer[SharedResourcesKey.Unauthorized]);

        return await _service.GetActiveParticipationsAsync(studentId, ct);
    }
}
