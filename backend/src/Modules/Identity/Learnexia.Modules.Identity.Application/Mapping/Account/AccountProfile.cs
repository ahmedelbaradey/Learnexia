using AutoMapper;
using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Modules.Identity.Domain.Entities;

namespace Learnexia.Modules.Identity.Application.Mapping.Account;

// Entity → account-profile response (P1-12 BE-1). Phone echoes the inherited Identity PhoneNumber;
// Country echoes Nationality; AvatarUrl is surfaced read-only (set later by the upload endpoint).
public class AccountProfile : Profile
{
    public AccountProfile()
    {
        CreateMap<User, AccountProfileResponse>()
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Nationality))
            .ForMember(dest => dest.AvatarUrl, opt => opt.MapFrom(src => src.AvatarUrl));
    }
}
