using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetSubjectsForGrade;

/// <summary>
/// Returns all subjects scoped to a single grade, identified by grade number (1–6).
/// Student-facing read query — not auto-validated (IQuery, not ICommand).
/// Route: GET /api/learning/Subjects/ForGrade?grade={n}
/// </summary>
public record GetSubjectsForGradeQuery : IQuery<BaseResponse<List<StudentSubjectDto>>>
{
    /// <summary>Grade number 1–6 supplied via ?grade= query parameter.</summary>
    public int Grade { get; init; }
}
