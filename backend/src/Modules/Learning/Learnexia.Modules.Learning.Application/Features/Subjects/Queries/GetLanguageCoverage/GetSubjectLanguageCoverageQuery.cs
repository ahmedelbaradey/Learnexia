using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Queries.GetLanguageCoverage;

/// <summary>
/// For a given grade, reports which (SubjectCode, Language) trees exist and flags gaps.
/// Expected set: 4 subject codes × {Ar, En} = 8 slots, MINUS Arabic/English pinned-language
/// exceptions (Arabic → ar only, English → en only) = 6 expected slots.
/// AdminOnly — returns all slots including inactive ones.
/// </summary>
public record GetSubjectLanguageCoverageQuery : IQuery<BaseResponse<SubjectLanguageCoverageReportDto>>
{
    public int GradeId { get; init; }
}
