using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectLessons;

/// <summary>
/// Returns a subject's units (ordered by SequenceOrder) each with their lessons (ordered by SequenceOrder).
/// Student-facing read query — not auto-validated (IQuery, not ICommand).
/// Route: GET /api/learning/Subjects/{id}/Lessons
/// </summary>
public record GetSubjectLessonsQuery : IQuery<BaseResponse<List<UnitWithLessonsDto>>>
{
    public int SubjectId { get; init; }
}
