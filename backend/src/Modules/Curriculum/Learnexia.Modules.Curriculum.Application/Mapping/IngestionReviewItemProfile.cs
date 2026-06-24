using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.IngestionReview.Dtos;
using Learnexia.Modules.Curriculum.Domain.Entities;

namespace Learnexia.Modules.Curriculum.Application.Mapping;

/// <summary>
/// AutoMapper profile for <see cref="IngestionReviewItem"/> entity mappings (BL-05-BE-7/BE-8).
///
/// <para>Mappings:
/// <list type="bullet">
///   <item><see cref="IngestionReviewItem"/> → <see cref="IngestionReviewItemDto"/> (admin review queue)</item>
/// </list>
/// </para>
/// </summary>
public sealed class IngestionReviewItemProfile : Profile
{
    public IngestionReviewItemProfile()
    {
        CreateMap<IngestionReviewItem, IngestionReviewItemDto>();
    }
}
