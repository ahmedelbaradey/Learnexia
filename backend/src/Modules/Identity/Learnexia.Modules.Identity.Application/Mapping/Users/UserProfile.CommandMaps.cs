using AutoMapper;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.AddUser;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUser;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUserPreferredLanguage;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.UpdateUserProfile;
using Learnexia.Modules.Identity.Domain.Entities;

namespace Learnexia.Modules.Identity.Application.Mapping.Users;

public partial class UserProfile
{
    public UserProfile()
    {
        AddUserMapping();
        EditUserMapping();
        GetUserPaginatedListMapping();
        GetUserByIdMapping();
        UpdateUserProfileMapping();
        GetUserProfileMapping();
    }

    private void AddUserMapping()
    {
        CreateMap<AddUserCommand, User>()
            .ForMember(dest => dest.Roles, opt => opt.Ignore());
    }

    private void EditUserMapping()
    {
        CreateMap<EditUserCommand, User>()
            .ForMember(dest => dest.Roles, opt => opt.Ignore());

        CreateMap<EditUserPreferredLanguageCommand, User>();
    }

    private void UpdateUserProfileMapping()
    {
        CreateMap<UpdateUserProfileCommand, User>();
    }
}
