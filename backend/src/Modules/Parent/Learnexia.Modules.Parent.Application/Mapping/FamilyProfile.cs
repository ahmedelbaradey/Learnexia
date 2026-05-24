using AutoMapper;
using Learnexia.Modules.Parent.Application.Features.AddChild;
using Learnexia.Modules.Parent.Application.Features.LinkChild;
using Learnexia.Modules.Parent.Application.Features.UpdateChild;
using Learnexia.Shared.Contracts.Identity;

namespace Learnexia.Modules.Parent.Application.Mapping;

// Maps the cross-module child profile (ChildProfile, from the IChildAccountService seam) onto the Parent
// module's family DTOs. The Parent module never sees the Identity User entity — only this shaped seam DTO.
public class FamilyProfile : Profile
{
    public FamilyProfile()
    {
        CreateMap<ChildProfile, AddedChildResponse>();
        CreateMap<ChildProfile, UpdatedChildResponse>();
        CreateMap<ChildProfile, LinkedChildResponse>();
    }
}
