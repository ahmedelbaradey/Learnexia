using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.CompleteAttempt;

/// <summary>
/// Command to transition an in-progress attempt to Completed, recomputing all aggregates
/// (AccuracyPercentage, DurationSeconds, HintsUsedCount, CompletedAt).
/// AttemptId comes from the route (injected by the controller).
/// StudentId is NEVER taken from the client — it is resolved from the authenticated JWT via ICurrentUserService.
/// </summary>
public record CompleteAttemptCommand : ICommand<BaseResponse<AttemptSummaryDto>>
{
    public int AttemptId { get; set; }
}
