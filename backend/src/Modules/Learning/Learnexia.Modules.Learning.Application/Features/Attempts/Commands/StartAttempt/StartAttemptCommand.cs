using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.StartAttempt;

/// <summary>
/// Command to start a quiz attempt for a lesson.
/// LessonId comes from the route; StudentId is NEVER taken from the client —
/// it is resolved from the authenticated JWT via ICurrentUserService in the handler.
/// </summary>
public record StartAttemptCommand : ICommand<BaseResponse<StartAttemptResponse>>
{
    public int LessonId { get; init; }
}
