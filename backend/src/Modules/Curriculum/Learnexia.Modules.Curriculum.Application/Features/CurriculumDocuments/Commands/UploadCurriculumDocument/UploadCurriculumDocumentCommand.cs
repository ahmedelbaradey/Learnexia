using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.AspNetCore.Http;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Commands.UploadCurriculumDocument;

/// <summary>
/// Uploads a curriculum document (PDF, DOCX, or image) with grade/subject/language/country metadata.
/// File type, size, and magic-byte validation run in the handler (byte-level checks cannot be
/// expressed as FluentValidation property rules). The command is still an ICommand so that any
/// metadata-level validators fire via ValidationBehavior (BL-01 BE-4).
///
/// <para>Mirror of <c>UploadAvatarCommand</c> — carries the multipart IFormFile. Admin-only.</para>
/// </summary>
public record UploadCurriculumDocumentCommand(
    IFormFile File,
    int GradeId,
    int SubjectId,
    ContentLanguage Language,
    string? Country)
    : ICommand<BaseResponse<CurriculumDocumentResponse>>;
