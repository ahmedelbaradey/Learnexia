using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

/// <summary>
/// AutoMapper profile for the skill dependency graph read model (P2-11 BE-5 + P7-03).
/// Auto-discovered via <c>AddAutoMapper(assembly)</c> in <c>AddLearningApplication</c>.
/// </summary>
public class KnowledgeGraphProfile : Profile
{
    public KnowledgeGraphProfile()
    {
        CreateMap<KnowledgeNode, KnowledgeNodeDto>()
            // IsSkillActive is computed in the handler (requires a skills DB look-up); ignore here.
            .ForMember(dest => dest.IsSkillActive, opt => opt.Ignore());

        // P7-03 (security): student-facing node DTO — omits IsSkillActive.
        // Used by GetPrerequisites + GetUnlockedBy (both student-reachable [Authorize] endpoints).
        CreateMap<KnowledgeNode, StudentKnowledgeNodeDto>();

        // P7-03: edge DTO mapping for GetGraphQuery.
        CreateMap<KnowledgeEdge, KnowledgeEdgeDto>();
    }
}
