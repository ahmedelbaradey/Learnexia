using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.AbandonAttempt;

/// <summary>
/// Command to transition an in-progress attempt to Abandoned, recomputing aggregates over
/// any answers captured so far (partial capture — BE-3 signal preservation).
/// AttemptId comes from the route (injected by the controller).
/// StudentId is NEVER taken from the client — it is resolved from the authenticated JWT via ICurrentUserService.
/// Zero answers is a valid case (accuracy = 0, no divide-by-zero).
/// </summary>
public record AbandonAttemptCommand : ICommand<BaseResponse<AttemptSummaryDto>>
{
    public int AttemptId { get; set; }
}
