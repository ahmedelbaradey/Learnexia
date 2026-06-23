using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.ReparseCurriculumDocument;

/// <summary>
/// Admin command to re-enqueue a parse job for an existing <c>CurriculumDocument</c> (BL-02-BE-4).
///
/// <para>Prerequisites:
/// <list type="bullet">
///   <item>Document must exist (404 otherwise).</item>
///   <item>No non-terminal parse job must currently be in flight (409 otherwise).</item>
/// </list>
/// </para>
///
/// <para>On success: document is reset to <c>Processing</c> and a fresh <c>Pending</c>
/// parse job is written inside an explicit transaction. Mirrors BL-01 BE-5's enqueue pattern.</para>
/// </summary>
public record ReparseCurriculumDocumentCommand(int DocumentId)
    : ICommand<BaseResponse<CurriculumDocumentResponse>>;
