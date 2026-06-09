using AutoMapper;
using Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;
using Learnexia.Modules.Gamification.Domain.Entities;

namespace Learnexia.Modules.Gamification.Application.Mapping;

/// <summary>
/// AutoMapper profile for the P7-13 admin surface.
/// Maps domain catalog entities to their admin-facing DTOs.
/// </summary>
public sealed class GamificationAdminProfile : Profile
{
    public GamificationAdminProfile()
    {
        CreateMap<BadgeDefinition, BadgeDefinitionDto>();
        CreateMap<MissionDefinition, MissionDefinitionDto>();
    }
}
