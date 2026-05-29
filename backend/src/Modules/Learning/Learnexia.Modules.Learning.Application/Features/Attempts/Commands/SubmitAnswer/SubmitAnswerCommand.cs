using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.SubmitAnswer;

/// <summary>
/// Command to submit a student's answer for a single question within an in-progress attempt.
/// AttemptId comes from the route (injected by the controller); StudentId is NEVER taken from
/// the client — it is resolved from the authenticated JWT via ICurrentUserService in the handler.
/// </summary>
public record SubmitAnswerCommand : SubmitAnswerDto, ICommand<BaseResponse<SubmitAnswerResponse>>
{
}
