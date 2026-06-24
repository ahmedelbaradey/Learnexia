using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Modules.Learning.Infrastructure.Behaviors;
using Learnexia.Modules.Learning.Infrastructure.Contracts;
using Learnexia.Modules.Learning.Infrastructure.Jobs;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Learning.Infrastructure.Repository;
using Learnexia.Modules.Learning.Infrastructure.Service;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Logging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Learning.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLearningInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext(configuration);
        services.AddHttpContextAccessor();

        services.AddSingleton<ILoggerManager, LoggerManager>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddScoped<ILearningRepository, LearningRepository>();
        services.AddScoped<ILearningRepositoryManager, LearningRepositoryManager>();
        services.AddScoped<ILearningServiceManager, LearningServiceManager>();

        // Stage 6: Attempts + Dashboard (Option C — no EF in Application)
        services.AddScoped<IAttemptWriteService, AttemptWriteService>();
        services.AddScoped<IAttemptQueryService, AttemptQueryService>();
        services.AddScoped<IStartAttemptService, StartAttemptService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();

        // P3-09: Internal mastery seam for P3-08/P3-10/P3-11 in-process consumers.
        services.AddScoped<IMasteryService, MasteryService>();

        // P3-13: Student behavioral profile engine options + service.
        // StudentProfileOptions lives in Domain (same pattern as AdaptivityOptions) so the
        // pure engine can reference it without violating the Application → Domain dependency direction.
        services.Configure<StudentProfileOptions>(
            configuration.GetSection(StudentProfileOptions.SectionName));
        services.AddScoped<IStudentProfileService, StudentProfileService>();

        // P3-13 Student profile recompute sweep job.
        // Transient — the job creates its own inner scope via IServiceScopeFactory.CreateAsyncScope()
        // so it does not participate in any caller's scope. Mirrors SpacedRepetitionSweepJob registration.
        services.AddTransient<StudentProfileRecomputeJob>();

        // P5-09a-BE-1: Recommendation Engine options (Domain, same pattern as AdaptivityOptions).
        // Placed before service registration so the options are available when the service is resolved.
        services.Configure<RecommendationOptions>(
            configuration.GetSection(RecommendationOptions.SectionName));

        // P5-09-BE-3: Recommendation computation + persistence service (Option C — EF only here).
        services.AddScoped<IRecommendationService, RecommendationService>();

        // P5-09-BE-4: Daily recommendation recompute job.
        // Transient — the job creates its own inner scope via IServiceScopeFactory.CreateAsyncScope().
        // Mirrors StudentProfileRecomputeJob registration.
        services.AddTransient<RecommendationRecomputeJob>();

        // P5-07-BE-1/2/3: Calibration Engine options (Domain, same pattern as AdaptivityOptions).
        // Placed before service registration so the options are available when the service is resolved.
        services.Configure<CalibrationOptions>(
            configuration.GetSection(CalibrationOptions.SectionName));

        // P5-07-BE-1/2/3: Calibration computation + persistence service (Option C — EF only here).
        services.AddScoped<ICalibrationService, CalibrationService>();

        // P5-07-BE-1: Daily calibration job.
        // Transient — the job creates its own inner scope via IServiceScopeFactory.CreateAsyncScope().
        // Mirrors RecommendationRecomputeJob registration.
        services.AddTransient<CalibrationJob>();

        // P3-08 Adaptivity Engine options + service.
        // AdaptivityOptions lives in Domain so the pure engine can reference it without violating
        // the Application → Domain dependency direction.
        services.Configure<AdaptivityOptions>(
            configuration.GetSection(AdaptivityOptions.SectionName));
        services.AddScoped<IAdaptivityService, AdaptivityService>();

        // P3-11 Quiz Selection Engine options.
        // QuizSelectionOptions lives in Domain (same pattern as AdaptivityOptions) so the
        // pure engine can reference it without violating the Application → Domain dependency direction.
        services.Configure<QuizSelectionOptions>(
            configuration.GetSection(QuizSelectionOptions.SectionName));

        // P3-10 Spaced-Repetition Engine options.
        // SpacedRepetitionOptions lives in Domain (same pattern as AdaptivityOptions) so the
        // pure engine can reference it without violating the Application → Domain dependency direction.
        services.Configure<SpacedRepetitionOptions>(
            configuration.GetSection(SpacedRepetitionOptions.SectionName));

        // P3-10 Spaced-Repetition sweep job.
        // Transient — the job creates its own inner scope via IServiceScopeFactory.CreateAsyncScope()
        // so it does not participate in any caller's scope. Mirrors StreakSweepJob registration.
        services.AddTransient<SpacedRepetitionSweepJob>();

        // BL-05 seam-impl: Cross-module write seam for curriculum ingest-advance (IPedagogicalTreeWriter).
        // Curriculum calls this interface to upsert Subject/Unit/Lesson/Concept/Skill/KnowledgeNode
        // without any project reference curriculum→learning (module isolation rule, CLAUDE.md rule 1).
        // Scoped: depends on scoped LearningDbContext.
        services.AddScoped<IPedagogicalTreeWriter, PedagogicalTreeWriterAdapter>();

        // P3-04 BE-2: Cross-module seam — allows the Ai module handler to read minimal lesson
        // metadata (title, subject, grade) from LearningDbContext via Shared.Contracts.
        // The Ai module depends only on ILessonContextContract from Shared.Contracts; it never
        // references Learning's projects directly (module isolation rule).
        services.AddScoped<ILessonContextContract, LessonContextContractAdapter>();

        // P3-05 BE-3: Cross-module seam — allows the Ai Hint handler to read CorrectAnswer
        // and the server-derived CurrentHintLevel (OQ-2b, OQ-4) from LearningDbContext via
        // Shared.Contracts. The Ai module depends only on IQuestionAnswerContract; no direct
        // reference to any Learning project (module isolation rule).
        services.AddScoped<IQuestionAnswerContract, QuestionAnswerContractAdapter>();

        // ── P5-08 + P5-02: Cross-module read seams for the Parent analytics API ──────────────────

        // P5-08-BE-2: windowed learning stats (lessons completed, time-learning, attempts).
        services.AddScoped<IStudentLearningStatsQuery, StudentLearningStatsQueryAdapter>();

        // P5-08-BE-3: per-subject mastery summary (overall + per-subject % from StudentSkillMastery).
        services.AddScoped<IStudentMasterySummaryQuery, StudentMasterySummaryQueryAdapter>();

        // P5-02-BE-1: internal detector service — all ranking logic lives here; adapters delegate to it.
        services.AddScoped<IWeakAreaDetectorService, WeakAreaDetectorService>();

        // P5-02-BE-2: all-subjects weak-area seam (Parent module E5 + P5-01 report consume this).
        services.AddScoped<IStudentAllSubjectsWeakAreasQuery, StudentAllSubjectsWeakAreasQueryAdapter>();

        // P5-02-BE-3: re-wire the Ai subject-scoped seam (IStudentWeakAreasQuery) from the
        // EmptyWeakAreasQuery placeholder to the real bridge that delegates to the detector.
        // The Host runs AddLearningModule BEFORE AddAiModule, so this registration lands FIRST; the
        // Ai module registers its EmptyWeakAreasQuery stub via TryAddTransient, which is then skipped
        // (the interface is already registered) — so this bridge wins when Learning is loaded.
        // The Ai module itself does NOT change; it continues to inject IStudentWeakAreasQuery.
        services.AddScoped<IStudentWeakAreasQuery, AiWeakAreasQueryBridge>();

        // P5-09-BE-5: Cross-module read seam — returns the latest persisted recommendation set for
        // a student. Consumed by the Parent analytics endpoint (BE-6) and P3-14 Lexi narration.
        services.AddScoped<IStudentRecommendationsQuery, StudentRecommendationsQueryAdapter>();

        // P7-10-BE: Platform-aggregate read seam — platform-wide learning stats for the admin KPI dashboard.
        // No studentId — returns aggregated counts for ALL students in the window.
        // Scoped: depends on scoped LearningDbContext.
        services.AddScoped<IPlatformLearningStatsQuery, PlatformLearningStatsQueryAdapter>();

        // Unit-of-Work behavior (ADR 0001 §2 + ADR 0002 §2): commit once per ICommand<>, then dispatch
        // domain events AFTER commit. Registered here in Infrastructure (not Application) because it
        // injects the concrete LearningDbContext. Registered AFTER ValidationBehavior (added in
        // AddLearningApplication, which is called before this) so validation rejects bad input first.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));

        return services;
    }

    public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<LearningDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", LearningDbContext.Schema)
                           .MigrationsAssembly(typeof(LearningDbContext).Assembly.FullName)));
    }
}
