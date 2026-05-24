using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Provides a single entry-point to all Learning module services.
/// Mirrors Catalog's <c>ServiceManager</c> pattern.
/// </summary>
public class LearningServiceManager : ILearningServiceManager
{
    private readonly Lazy<IGradeService> _gradeService;
    private readonly Lazy<ISubjectService> _subjectService;
    private readonly Lazy<IUnitService> _unitService;
    private readonly Lazy<ILessonService> _lessonService;
    private readonly Lazy<IConceptService> _conceptService;
    private readonly Lazy<ISkillService> _skillService;
    private readonly Lazy<IAttemptService> _attemptService;
    private readonly Lazy<IQuizQuestionService> _quizQuestionService;

    public LearningServiceManager(
        ILearningRepository repository,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        LearningDbContext dbContext)
    {
        _gradeService        = new Lazy<IGradeService>(() => new GradeService(repository, mapper, localizer));
        _subjectService      = new Lazy<ISubjectService>(() => new SubjectService(repository, mapper, localizer));
        _unitService         = new Lazy<IUnitService>(() => new UnitService(repository, mapper, localizer));
        _lessonService       = new Lazy<ILessonService>(() => new LessonService(repository, mapper, localizer));
        _conceptService      = new Lazy<IConceptService>(() => new ConceptService(repository, mapper, localizer));
        _skillService        = new Lazy<ISkillService>(() => new SkillService(repository, mapper, localizer));
        _attemptService      = new Lazy<IAttemptService>(() => new AttemptService(repository, mapper, localizer, dbContext));
        _quizQuestionService = new Lazy<IQuizQuestionService>(() => new QuizQuestionService(repository, mapper, localizer));
    }

    public IGradeService GradeService             => _gradeService.Value;
    public ISubjectService SubjectService         => _subjectService.Value;
    public IUnitService UnitService               => _unitService.Value;
    public ILessonService LessonService           => _lessonService.Value;
    public IConceptService ConceptService         => _conceptService.Value;
    public ISkillService SkillService             => _skillService.Value;
    public IAttemptService AttemptService         => _attemptService.Value;
    public IQuizQuestionService QuizQuestionService => _quizQuestionService.Value;
}
