using AutoMapper;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.Responses;
using Learnexia.Modules.Identity.Domain.Entities;

namespace Learnexia.Modules.Identity.Application.Mapping.Authorizations;

public class AuthorizationProfile : Profile
{
    public AuthorizationProfile()
    {
        CreateMap<Role, GetRoleByIdResponse>()
            .ForMember(dest => dest.roleName, opt => opt.MapFrom(src => src.Name));

        CreateMap<Role, GetRoleListResponse>()
            .ForMember(dest => dest.roleName, opt => opt.MapFrom(src => src.Name));
    }
}
