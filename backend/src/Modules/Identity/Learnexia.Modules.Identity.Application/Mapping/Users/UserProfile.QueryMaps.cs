using AutoMapper;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Modules.Identity.Domain.Entities;

namespace Learnexia.Modules.Identity.Application.Mapping.Users;

public partial class UserProfile : Profile
{
    private void GetUserByIdMapping()
    {
        CreateMap<User, GetUserResponse>();
    }

    private void GetUserPaginatedListMapping()
    {
        CreateMap<User, GetUserListResponse>()
            .ForMember(dest => dest.Roles, opt => opt.MapFrom(src => src.Roles.Select(r => r.Name).ToList()))
            .ForMember(dest => dest.PrimaryRole, opt => opt.MapFrom(src => src.Roles.Select(r => r.Name).FirstOrDefault()))
            .ForMember(dest => dest.LastUpdateDate, opt => opt.MapFrom(src => src.UpdatedAt));
    }

    private void GetUserProfileMapping()
    {
        CreateMap<User, UserProfileResponseDto>()
            .ForMember(dest => dest.CVFilePath, opt => opt.Ignore())
            .ForMember(dest => dest.PersonalPhotoPath, opt => opt.Ignore())
            .ForMember(dest => dest.PreviewUrl, opt => opt.Ignore());
    }
}
