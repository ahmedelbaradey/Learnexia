using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Queries.GetCurriculumDocument;

/// <summary>
/// Returns the full detail of a single curriculum document by id.
/// Returns 404 when the id is not found.
///
/// <para>This is an <see cref="IQuery{TResponse}"/> — ValidationBehavior does NOT run.
/// The handler guards the id.</para>
/// </summary>
public class GetCurriculumDocumentQuery : IQuery<BaseResponse<CurriculumDocumentDto>>
{
    public int Id { get; init; }
}
