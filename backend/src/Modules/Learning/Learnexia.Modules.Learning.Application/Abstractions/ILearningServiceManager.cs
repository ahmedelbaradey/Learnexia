namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface ILearningServiceManager
{
    IGradeService GradeService { get; }
    ISubjectService SubjectService { get; }
    IUnitService UnitService { get; }
    ILessonService LessonService { get; }
    IConceptService ConceptService { get; }
    ISkillService SkillService { get; }
}
