using AutoMapper;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.LinkChild;
using Learnexia.Modules.Identity.Domain.Entities;

namespace Learnexia.Modules.Identity.Application.Mapping.Family;

public class FamilyProfile : Profile
{
    public FamilyProfile()
    {
        // Entity → response. Email may be null on IdentityUser in theory; coalesce to empty.
        CreateMap<User, LinkedChildResponse>()
            .ForMember(dest => dest.Email, opt => opt.MapFrom(src => src.Email ?? string.Empty));
    }
}
