using AutoMapper;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.AddChild;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.UpdateChild;
using Learnexia.Modules.Identity.Domain.Entities;

namespace Learnexia.Modules.Identity.Application.Mapping.Family;

public class FamilyProfile : Profile
{
    public FamilyProfile()
    {
        // Entity → response. Email may be null on IdentityUser in theory; coalesce to empty.
        CreateMap<User, LinkedChildResponse>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty));

        // Add-Child response (P1-03). Surfaces grade/language/country; Language echoes the stored
        // PreferredLanguage culture code, Country echoes Nationality. LinkedChildResponse is left
        // untouched so the P1-04 My-Children contract is unchanged.
        CreateMap<User, AddedChildResponse>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty))
            .ForMember(dest => dest.Language, opt => opt.MapFrom(src => src.PreferredLanguage))
            .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Nationality ?? string.Empty));

        // Update-Child response (BE-8). Same projection as AddedChildResponse so the FE list refreshes
        // in place: Language echoes the stored PreferredLanguage culture code, Country echoes Nationality.
        CreateMap<User, UpdatedChildResponse>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty))
            .ForMember(dest => dest.Language, opt => opt.MapFrom(src => src.PreferredLanguage))
            .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Nationality ?? string.Empty));
    }
}
