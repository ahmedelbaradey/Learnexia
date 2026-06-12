using Learnexia.Modules.Learning.Application.Features.Attempts.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Queries.GetStudentAttempts;

/// <summary>
/// Returns the list of attempts for a given student, ordered by StartedAt descending.
/// Queries are NOT auto-validated — validation is performed inline in the handler.
/// StudentId is supplied via the route; the handler allows the owning student (JWT match — IDOR guard)
/// or a parent linked to that student (IParentChildQuery seam), generic 403 otherwise.
/// </summary>
public record GetStudentAttemptsQuery : IQuery<BaseResponse<List<AttemptListItemDto>>>
{
    public int StudentId { get; set; }
}
