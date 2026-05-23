using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Concepts.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Concepts.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

public class ConceptsProfile : Profile
{
    public ConceptsProfile()
    {
        CreateMap<AddConceptCommand, Concept>();
        CreateMap<EditConceptCommand, Concept>();
        CreateMap<Concept, SingleConceptResponse>();
    }
}
