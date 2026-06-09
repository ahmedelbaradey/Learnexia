using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Subjects.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

public class SubjectsProfile : Profile
{
    public SubjectsProfile()
    {
        // P7-SEC-4: IsActive and SequenceOrder must not be settable via Create/Update —
        // those are controlled exclusively by SetActive and Reorder endpoints.
        // IsActive defaults to true (entity default); SequenceOrder defaults to 0.
        CreateMap<AddSubjectCommand, Subject>()
            .ForMember(d => d.IsActive, opt => opt.Ignore())
            .ForMember(d => d.SequenceOrder, opt => opt.Ignore());
        // P2-QC-DEFECT-1: SubjectCode and Language are the natural key of the subject tree
        // (GradeId, SubjectCode, Language). Changing them on edit would silently move the tree
        // to a different slot and risk a unique-index collision. Keep them immutable — only Name,
        // Country, and GradeId can be changed via Edit; SubjectCode/Language are set at Create time.
        CreateMap<EditSubjectCommand, Subject>()
            .ForMember(d => d.IsActive, opt => opt.Ignore())
            .ForMember(d => d.SequenceOrder, opt => opt.Ignore())
            .ForMember(d => d.SubjectCode, opt => opt.Ignore())
            .ForMember(d => d.Language, opt => opt.Ignore());

        // P7-01: Subject → SubjectDto (admin DTO with code/language/order/active).
        // SingleSubjectResponse inherits SubjectDto so this mapping covers it.
        CreateMap<Subject, SubjectDto>();
        CreateMap<Subject, SingleSubjectResponse>();
    }
}
