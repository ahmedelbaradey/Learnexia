using AutoMapper;
using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Modules.Identity.Domain.Entities;

namespace Learnexia.Modules.Identity.Application.Mapping.Account;

// Entity → account-profile response (P1-12 BE-1). Phone echoes the inherited Identity PhoneNumber;
// Country echoes Nationality. AvatarUrl is IGNORED here: User.AvatarUrl stores the storage OBJECT KEY,
// and the handler turns it into a freshly presigned GET URL via IStorageService (a pure compute), so
// it must not be copied verbatim by the mapper.
public class AccountProfile : Profile
{
    public AccountProfile()
    {
        CreateMap<User, AccountProfileResponse>()
            .ForMember(dest => dest.Phone, opt => opt.MapFrom(src => src.PhoneNumber))
            .ForMember(dest => dest.Country, opt => opt.MapFrom(src => src.Nationality))
            .ForMember(dest => dest.AvatarUrl, opt => opt.Ignore());
    }
}
