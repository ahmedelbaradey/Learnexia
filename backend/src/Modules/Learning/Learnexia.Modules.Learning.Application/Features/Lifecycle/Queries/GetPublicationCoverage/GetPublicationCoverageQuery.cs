using Learnexia.Modules.Learning.Application.Features.Lifecycle.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Learning.Application.Features.Lifecycle.Queries.GetPublicationCoverage;

/// <summary>
/// Returns publication-coverage for all (SubjectCode, Language) slots in a grade.
/// Complements P7-01-BE-8 existence coverage with lifecycle/publish-state coverage.
/// Shows which language trees are Published (live to students) vs Draft/Archived.
/// AdminOnly — enforced at the controller layer.
/// </summary>
public record GetPublicationCoverageQuery : IQuery<BaseResponse<PublicationCoverageReportDto>>
{
    public int GradeId { get; init; }
}
