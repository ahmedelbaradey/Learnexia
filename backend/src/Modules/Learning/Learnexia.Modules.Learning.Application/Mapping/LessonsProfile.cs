using AutoMapper;
using Learnexia.Modules.Learning.Application.Features.ContentBlocks.Dtos;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;
using Learnexia.Modules.Learning.Application.Features.Lessons.Dtos;
using Learnexia.Modules.Learning.Domain.Entities;

namespace Learnexia.Modules.Learning.Application.Mapping;

public class LessonsProfile : Profile
{
    public LessonsProfile()
    {
        // P7-SEC-4: IsActive, SequenceOrder, and IsDeleted must not be settable via Create/Update —
        // those are controlled exclusively by SetLessonActive, ReorderLessons, and DeleteLesson commands.
        // IsActive defaults to true (entity default); SequenceOrder defaults to 0.
        CreateMap<AddLessonCommand, Lesson>()
            .ForMember(d => d.IsActive, opt => opt.Ignore())
            .ForMember(d => d.SequenceOrder, opt => opt.Ignore())
            .ForMember(d => d.IsDeleted, opt => opt.Ignore());
        CreateMap<EditLessonCommand, Lesson>()
            .ForMember(d => d.IsActive, opt => opt.Ignore())
            .ForMember(d => d.SequenceOrder, opt => opt.Ignore())
            .ForMember(d => d.IsDeleted, opt => opt.Ignore());

        // Lesson → SingleLessonResponse: Explanation + Visual auto-map by name (P2-05 new columns).
        // QuickCheck is filled manually in GetLessonQueryHandler — NOT by AutoMapper from Lesson
        // (Lesson has no QuickCheck nav property). CorrectAnswer exclusion is in QuizProfile.
        //
        // P7-SEC-5 (post security-audit): EstimatedMinutes is admin metadata newly added in P7-02.
        // It must NOT be exposed on the student-facing SingleLessonResponse (would leak lesson duration
        // to students, and the P2-05 FE has no dependency on it). Excluded here via Ignore().
        // The pre-existing structural fields (UnitId, SequenceOrder, SkillId) are retained as-is to
        // avoid breaking the P2-05 renderer.
        // TODO (data-minimization follow-up): review all LessonDto fields (UnitId/SequenceOrder/SkillId)
        // for student-facing data-minimization when the admin/student DTO split is formalised.
        CreateMap<Lesson, SingleLessonResponse>()
            .ForMember(dest => dest.QuickCheck, opt => opt.Ignore())
            .ForMember(dest => dest.EstimatedMinutes, opt => opt.Ignore());

        // P7-02: Admin detail response — maps IsActive, EstimatedMinutes, and ContentBlocks.
        // All non-deleted blocks (IsDeleted == null or false) are included, ordered by SequenceOrder.
        // IsActive is intentionally NOT filtered here — admin sees inactive blocks too.
        CreateMap<Lesson, AdminLessonDetailDto>()
            .ForMember(dest => dest.ContentBlocks, opt => opt.MapFrom(src =>
                src.ContentBlocks.Where(cb => cb.IsDeleted != true).OrderBy(cb => cb.SequenceOrder).ToList()));
    }
}
