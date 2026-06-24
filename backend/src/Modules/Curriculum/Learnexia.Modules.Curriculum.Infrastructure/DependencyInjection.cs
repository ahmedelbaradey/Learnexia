using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Infrastructure.Configuration;
using Learnexia.Modules.Curriculum.Infrastructure.Features.Retrieval;
using Learnexia.Modules.Curriculum.Infrastructure.Jobs;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Contracts.AiTutor;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pgvector.EntityFrameworkCore;

namespace Learnexia.Modules.Curriculum.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCurriculumInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext — Npgsql/PostgreSQL, schema = "curriculum", migrations history in that schema.
        // UseVector() enables the pgvector EF Core plugin so that vector(1024) columns are handled.
        // Mirrors AiDbContext registration in Ai's DependencyInjection.cs.
        services.AddDbContext<CurriculumDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("Default"),
                       builder => builder
                           .UseVector()
                           .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                           .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName)));

        services.AddSingleton<ILoggerManager, LoggerManager>();

        // ── BL-01: ICurrentUserService for the upload command handler ─────────────────────────────
        // Each module owns its own CurrentUserService implementation (module isolation — no cross-module
        // project references). Mirrors Learning's AddScoped<ICurrentUserService, CurrentUserService>().
        services.AddScoped<ICurrentUserService, CurriculumCurrentUserService>();

        // ── BE-6: EmbeddingSettings + BgeM3EmbeddingProvider typed HttpClient ───────────────────
        // Mirrors Notifications.AddPushSender / ExpoPushSender pattern.
        // BaseAddress is set from EmbeddingSettings.BaseUrl.
        // When BaseUrl is empty (no live TEI endpoint yet) the provider fail-softs and returns null.
        services.AddOptions<EmbeddingSettings>()
            .Bind(configuration.GetSection(EmbeddingSettings.SectionName));

        var embeddingSection = configuration.GetSection(EmbeddingSettings.SectionName)
            .Get<EmbeddingSettings>() ?? new EmbeddingSettings();

        var httpClientBuilder = services.AddHttpClient<BgeM3EmbeddingProvider>(c =>
        {
            if (!string.IsNullOrWhiteSpace(embeddingSection.BaseUrl))
            {
                c.BaseAddress = new Uri(embeddingSection.BaseUrl);
            }
            // Bound worst-case embedding latency per NFR-1 budget (< 4 s end-to-end).
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IEmbeddingProvider, BgeM3EmbeddingProvider>();

        // ── BE-7: RetrievalSettings (similarity floor) ────────────────────────────────────────────
        services.AddOptions<RetrievalSettings>()
            .Bind(configuration.GetSection(RetrievalSettings.SectionName));

        // ── BE-11: RagContextProvider / ICurriculumContextQuery ─────────────────────────────────
        // Register behind AiHelper:ContextProvider = "Rag" config key.
        // When the key is "Rag", override the stubs registered by Ai Application DI.
        // Note: AddAiModule runs BEFORE AddCurriculumModule in Program.cs. The Ai Application
        // registers ILearningContextProvider with TryAddTransient (stub) and ICurriculumContextQuery
        // with AddTransient (stub). Adding a second registration here via AddTransient makes the
        // Curriculum registration the LAST registered — .NET DI resolves the last registration
        // for a single-injection service, effectively overriding the stub.
        var contextProviderKey = configuration["AiHelper:ContextProvider"] ?? string.Empty;
        if (contextProviderKey.Equals("Rag", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<RagContextProvider>();
            services.AddScoped<ILearningContextProvider, RagContextProvider>();
            services.AddScoped<ICurriculumContextQuery, RagContextProvider>();
        }

        // ── BL-01 BE-3: CurriculumUploadConfiguration (100 MB cap + curriculum bucket name) ────────
        // Bound from "CurriculumUpload" appsettings section. Injected into the upload command handler.
        services.AddOptions<CurriculumUploadConfiguration>()
            .Bind(configuration.GetSection(CurriculumUploadConfiguration.Section));

        // ── BL-01 Q1: Bucket-ensure service ──────────────────────────────────────────────────────
        // Called at startup via CurriculumModule.InitializeAsync to provision the curriculum bucket.
        // Scoped: resolves IStorageService (typed HttpClient, scoped) inside a startup scope.
        services.AddScoped<CurriculumBucketEnsureService>();

        // ── BE-9: CurriculumChunkSeeder — static class; called directly via CurriculumModule.InitializeAsync ──
        // No DI registration needed — CurriculumChunkSeeder.SeedAsync(IServiceProvider) is called
        // as a static method from CurriculumModule.InitializeAsync, not via the DI container.

        // ── WI-A2: ReEmbedCurriculumJob (Hangfire, admin-triggered re-embed) ──────────────────────
        // Transient — mirrors GamificationCacheRebuildJob registration pattern.
        // Creates its own inner scope via IServiceScopeFactory so scoped deps (DbContext,
        // IEmbeddingProvider) are always resolved in a fresh, non-HTTP scope.
        services.AddTransient<ReEmbedCurriculumJob>();

        // ── BL-02 BE-2: IParsingServiceClient — mock-only test seam (Q10, ADR-0004) ─────────────
        // No live HTTP call — the Python worker polls the DB-outbox directly.
        // NoOpParsingServiceClient is the only registered implementation.
        // The api-tester replaces this with a mock that returns a seeded ParseArtifactResult.
        services.AddScoped<IParsingServiceClient, NoOpParsingServiceClient>();

        // ── BL-02 BE-7: PipelineJobPollerConfiguration + ParseJobAdvanceService ──────────────────
        // PipelineJobPollerConfiguration is bound from the "CurriculumPipeline" appsettings section.
        // ParseJobAdvanceService is a BackgroundService (hosted service) that polls PipelineJobs
        // WHERE Status IN ('Done','Failed') AND JobType='parse' using FOR UPDATE SKIP LOCKED.
        // It owns the .NET retry policy (RetryCount/MaxRetries/PermanentlyFailed — ADR-0004 Q7).
        services.AddOptions<PipelineJobPollerConfiguration>()
            .Bind(configuration.GetSection(PipelineJobPollerConfiguration.Section));

        services.AddSingleton<ParseJobAdvanceService>();
        services.AddHostedService(sp => sp.GetRequiredService<ParseJobAdvanceService>());

        // ── BL-05 BE-3: IIngestionServiceClient — mock-only test seam ─────────────────────────
        // Mirrors IParsingServiceClient / NoOpParsingServiceClient pattern.
        // NoOpIngestionServiceClient always returns null (Python worker writes ResultJson to DB).
        services.AddScoped<IIngestionServiceClient, NoOpIngestionServiceClient>();

        // ── BL-05 BE-12: ICurriculumVersionResolver + CurriculumVersionResolver ─────────────────
        // Draft-only version resolver: finds or creates the Draft CurriculumVersion for
        // (SubjectId, Language). Called by IngestJobAdvanceService per job.
        services.AddScoped<ICurriculumVersionResolver, CurriculumVersionResolver>();

        // ── BL-05 BE-13: IngestJobPollerConfiguration + IngestJobAdvanceService ─────────────────
        // IngestJobPollerConfiguration is bound from the same "CurriculumPipeline" section (separate keys).
        // IngestJobAdvanceService is a BackgroundService that polls PipelineJobs
        // WHERE Status IN ('Done','Failed') AND JobType='ingest' using FOR UPDATE SKIP LOCKED.
        // Resolves IPedagogicalTreeWriter from the Learning module via IServiceProvider
        // (cross-module seam — BL-05 Batch 2a-0 and Q1 decision).
        services.AddOptions<IngestJobPollerConfiguration>()
            .Bind(configuration.GetSection(IngestJobPollerConfiguration.Section));

        services.AddSingleton<IngestJobAdvanceService>();
        services.AddHostedService(sp => sp.GetRequiredService<IngestJobAdvanceService>());

        // ── BL-03 BE-2: IGraphInferenceClient — mock-only test seam ──────────────────────────────
        // No live HTTP call to Python (DB-outbox lane; Python polls PipelineJobs).
        // Mirrors IParsingServiceClient / NoOpParsingServiceClient registration.
        services.AddScoped<IGraphInferenceClient, NoOpGraphInferenceClient>();

        // ── BL-03 BE-3: InferEdgesJobPollerConfiguration + EdgeInferenceAdvanceService ──────────
        // InferEdgesJobPollerConfiguration is bound from the same "CurriculumPipeline" section
        // (separate keys: InferEdgesPollerIntervalSeconds, InferEdgesMaxRetries).
        // EdgeInferenceAdvanceService is a BackgroundService that claims Done/Failed
        // PipelineJobs WHERE JobType='infer_edges', deserializes ResultJson, resolves SkillKeys via
        // IKnowledgeNodeReader (cross-module seam), clamps strength/confidence server-side, and
        // idempotent-inserts KGSuggestion{Pending}. Never writes KnowledgeEdge (Decision E).
        services.AddOptions<InferEdgesJobPollerConfiguration>()
            .Bind(configuration.GetSection(InferEdgesJobPollerConfiguration.Section));

        services.AddSingleton<EdgeInferenceAdvanceService>();
        services.AddHostedService(sp => sp.GetRequiredService<EdgeInferenceAdvanceService>());

        return services;
    }
}
