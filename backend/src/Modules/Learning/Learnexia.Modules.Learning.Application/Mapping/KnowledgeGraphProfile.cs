using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.KnowledgeGraph.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

/// <summary>
/// AutoMapper profile for the skill dependency graph read model (P2-11 BE-5).
/// Auto-discovered via <c>AddAutoMapper(assembly)</c> in <c>AddLearningApplication</c>.
/// </summary>
public class KnowledgeGraphProfile : Profile
{
    public KnowledgeGraphProfile()
    {
        CreateMap<KnowledgeNode, KnowledgeNodeDto>();
    }
}
