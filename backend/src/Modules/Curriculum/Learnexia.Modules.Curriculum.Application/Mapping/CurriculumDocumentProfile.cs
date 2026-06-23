using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.CurriculumDocuments.Dtos;
using Learnexia.Modules.Curriculum.Domain.Entities;

namespace Learnexia.Modules.Curriculum.Application.Mapping;

/// <summary>
/// AutoMapper profile for <see cref="CurriculumDocument"/> entity mappings (BL-01 BE-4/BE-6/BE-7).
///
/// <para>Mappings:
/// <list type="bullet">
///   <item><see cref="CurriculumDocument"/> → <see cref="CurriculumDocumentResponse"/> (upload 201 response)</item>
///   <item><see cref="CurriculumDocument"/> → <see cref="CurriculumDocumentListDto"/> (paged list)</item>
///   <item><see cref="CurriculumDocument"/> → <see cref="CurriculumDocumentDto"/> (detail)</item>
/// </list>
/// </para>
///
/// <para><strong>Security:</strong> <c>ObjectKey</c> is mapped only to the detail DTO
/// (<see cref="CurriculumDocumentDto"/>), which is gated AdminOnly. The list and upload-response
/// DTOs omit <c>ObjectKey</c> (least-exposure principle).</para>
/// </summary>
public sealed class CurriculumDocumentProfile : Profile
{
    public CurriculumDocumentProfile()
    {
        // Upload response — ObjectKey omitted (not needed by caller; presign is at the read layer).
        CreateMap<CurriculumDocument, CurriculumDocumentResponse>();

        // List DTO — ObjectKey omitted (least exposure; admin sees key via detail endpoint).
        CreateMap<CurriculumDocument, CurriculumDocumentListDto>();

        // Detail DTO — includes ObjectKey (admin-only endpoint; security-auditor confirmed).
        CreateMap<CurriculumDocument, CurriculumDocumentDto>();
    }
}
