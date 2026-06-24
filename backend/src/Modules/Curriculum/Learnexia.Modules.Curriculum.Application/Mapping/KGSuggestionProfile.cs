using AutoMapper;
using Learnexia.Modules.Curriculum.Application.Features.KGSuggestions.Dtos;
using Learnexia.Modules.Curriculum.Domain.Entities;

namespace Learnexia.Modules.Curriculum.Application.Mapping;

/// <summary>
/// AutoMapper profile for <see cref="KGSuggestion"/> entity mappings (BL-03-BE-7/BE-8/BE-9).
///
/// <para>Mappings:
/// <list type="bullet">
///   <item><see cref="KGSuggestion"/> → <see cref="KGSuggestionDto"/> (admin suggestion queue)</item>
/// </list>
/// </para>
/// </summary>
public sealed class KGSuggestionProfile : Profile
{
    public KGSuggestionProfile()
    {
        CreateMap<KGSuggestion, KGSuggestionDto>();
    }
}
