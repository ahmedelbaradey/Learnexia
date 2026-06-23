using Learnexia.Modules.Curriculum.Domain.Enums;
using Microsoft.AspNetCore.Http;

namespace Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;

/// <summary>
/// Input DTO for uploading a curriculum document (BL-01 BE-4).
/// Carries the multipart file and metadata (grade, subject, language, country).
///
/// The file is NOT FluentValidation-validated (byte-level checks cannot be expressed as
/// property rules); type, size, and magic-byte validation run in the handler.
/// </summary>
public record UploadCurriculumDocumentDto(
    IFormFile File,
    int GradeId,
    int SubjectId,
    ContentLanguage Language,
    string? Country);
