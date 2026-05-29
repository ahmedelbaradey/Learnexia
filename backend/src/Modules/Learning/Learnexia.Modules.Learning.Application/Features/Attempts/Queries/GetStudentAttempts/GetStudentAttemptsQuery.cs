using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetStudentAttempts;

/// <summary>
/// Returns the list of attempts for a given student, ordered by StartedAt descending.
/// Queries are NOT auto-validated — validation is performed inline in the handler.
/// StudentId is supplied via the route and checked against the JWT in the handler (IDOR guard).
/// </summary>
public record GetStudentAttemptsQuery : IQuery<BaseResponse<List<AttemptListItemDto>>>
{
    public int StudentId { get; set; }
}
