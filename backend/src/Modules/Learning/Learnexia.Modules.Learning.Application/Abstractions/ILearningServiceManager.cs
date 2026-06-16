namespace Learnexia.Modules.Learning.Application.Abstractions;

public interface ILearningServiceManager
{
    IGradeService GradeService { get; }
    ISubjectService SubjectService { get; }
    IUnitService UnitService { get; }
    ILessonService LessonService { get; }
    IContentBlockService ContentBlockService { get; }
    IConceptService ConceptService { get; }
    ISkillService SkillService { get; }
    IAttemptService AttemptService { get; }
    IQuizQuestionService QuizQuestionService { get; }
    IKnowledgeGraphService KnowledgeGraphService { get; }

    // Stage 5: Lifecycle + Progress + Mastery + Reviews
    ILifecycleService LifecycleService { get; }
    IProgressService ProgressService { get; }
    IMasteryQueryService MasteryQueryService { get; }
    IReviewsService ReviewsService { get; }

    // Stage 6: Attempts + Dashboard
    IAttemptWriteService AttemptWriteService { get; }
    IAttemptQueryService AttemptQueryService { get; }
    IStartAttemptService StartAttemptService { get; }
    IDashboardQueryService DashboardQueryService { get; }
}
