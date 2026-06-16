using AutoMapper;
using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
    private readonly Lazy<IContentBlockService> _contentBlockService;
    private readonly Lazy<IConceptService> _conceptService;
    private readonly Lazy<ISkillService> _skillService;
    private readonly Lazy<IAttemptService> _attemptService;
    private readonly Lazy<IQuizQuestionService> _quizQuestionService;
    private readonly Lazy<IKnowledgeGraphService> _knowledgeGraphService;

    // Stage 5: Lifecycle + Progress + Mastery + Reviews
    private readonly Lazy<ILifecycleService> _lifecycleService;
    private readonly Lazy<IProgressService> _progressService;
    private readonly Lazy<IMasteryQueryService> _masteryQueryService;
    private readonly Lazy<IReviewsService> _reviewsService;

    // Stage 6: Attempts + Dashboard
    private readonly Lazy<IAttemptWriteService> _attemptWriteService;
    private readonly Lazy<IAttemptQueryService> _attemptQueryService;
    private readonly Lazy<IStartAttemptService> _startAttemptService;
    private readonly Lazy<IDashboardQueryService> _dashboardQueryService;

    public LearningServiceManager(
        ILearningRepository repository,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        LearningDbContext dbContext,
        ILoggerManager logger,
        IOptions<SpacedRepetitionOptions> srOptions)
    {
        _gradeService           = new Lazy<IGradeService>(() => new GradeService(repository, mapper, localizer));
        _subjectService         = new Lazy<ISubjectService>(() => new SubjectService(repository, mapper, localizer));
        _unitService            = new Lazy<IUnitService>(() => new UnitService(repository, mapper, localizer));
        _lessonService          = new Lazy<ILessonService>(() => new LessonService(repository, mapper, localizer));
        _contentBlockService    = new Lazy<IContentBlockService>(() => new ContentBlockService(repository, mapper, localizer));
        _conceptService         = new Lazy<IConceptService>(() => new ConceptService(repository, mapper, localizer));
        _skillService           = new Lazy<ISkillService>(() => new SkillService(repository, mapper, localizer));
        _attemptService         = new Lazy<IAttemptService>(() => new AttemptService(repository, mapper, localizer, dbContext));
        _quizQuestionService    = new Lazy<IQuizQuestionService>(() => new QuizQuestionService(repository, mapper, localizer));
        _knowledgeGraphService  = new Lazy<IKnowledgeGraphService>(() => new KnowledgeGraphService(repository, mapper, localizer));

        // Stage 5
        _lifecycleService    = new Lazy<ILifecycleService>(() => new LifecycleService(repository, mapper, localizer));
        _progressService     = new Lazy<IProgressService>(() => new ProgressService(repository, mapper, localizer));
        _masteryQueryService = new Lazy<IMasteryQueryService>(() => new MasteryQueryService(repository, mapper, localizer));
        _reviewsService      = new Lazy<IReviewsService>(() => new ReviewsService(repository, mapper, localizer));

        // Stage 6
        _attemptWriteService  = new Lazy<IAttemptWriteService>(() => new AttemptWriteService(repository, dbContext, logger, srOptions));
        _attemptQueryService  = new Lazy<IAttemptQueryService>(() => new AttemptQueryService(dbContext));
        _startAttemptService  = new Lazy<IStartAttemptService>(() => new StartAttemptService(dbContext));
        _dashboardQueryService = new Lazy<IDashboardQueryService>(() => new DashboardQueryService(dbContext, repository));
    }

    public IGradeService GradeService                   => _gradeService.Value;
    public ISubjectService SubjectService               => _subjectService.Value;
    public IUnitService UnitService                     => _unitService.Value;
    public ILessonService LessonService                 => _lessonService.Value;
    public IContentBlockService ContentBlockService     => _contentBlockService.Value;
    public IConceptService ConceptService               => _conceptService.Value;
    public ISkillService SkillService                   => _skillService.Value;
    public IAttemptService AttemptService               => _attemptService.Value;
    public IQuizQuestionService QuizQuestionService     => _quizQuestionService.Value;
    public IKnowledgeGraphService KnowledgeGraphService => _knowledgeGraphService.Value;

    // Stage 5
    public ILifecycleService LifecycleService       => _lifecycleService.Value;
    public IProgressService ProgressService         => _progressService.Value;
    public IMasteryQueryService MasteryQueryService => _masteryQueryService.Value;
    public IReviewsService ReviewsService           => _reviewsService.Value;

    // Stage 6
    public IAttemptWriteService AttemptWriteService     => _attemptWriteService.Value;
    public IAttemptQueryService AttemptQueryService     => _attemptQueryService.Value;
    public IStartAttemptService StartAttemptService     => _startAttemptService.Value;
    public IDashboardQueryService DashboardQueryService => _dashboardQueryService.Value;
}
