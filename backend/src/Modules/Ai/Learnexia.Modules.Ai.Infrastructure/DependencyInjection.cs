using Learnexia.Modules.Ai.Application;
using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.Safety;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Modules.Ai.Domain.Safety;
using Learnexia.Modules.Ai.Infrastructure.Cache;
using Learnexia.Modules.Ai.Infrastructure.Gateway;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Modules.Ai.Infrastructure.Providers;
using Learnexia.Modules.Ai.Infrastructure.Readiness;
using Learnexia.Modules.Ai.Infrastructure.Safety;
using Learnexia.Modules.Ai.Infrastructure.Services;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Learnexia.Modules.Ai.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // DbContext — Npgsql/PostgreSQL, schema = "ai", migrations history in that schema.
        // Mirrors ModerationDbContext registration in Moderation's DependencyInjection.cs.
        services.AddDbContext<AiDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("Default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", AiDbContext.Schema)
                           .MigrationsAssembly(typeof(AiDbContext).Assembly.FullName)));

        // Options — binds Ai:Gateway config section.
        services.Configure<AiGatewayOptions>(
            configuration.GetSection(AiGatewayOptions.SectionName));

        // Options — binds Ai:Safety config section (FR-AI-4 — all checks enabled by default).
        services.Configure<SafetyOptions>(
            configuration.GetSection(SafetyOptions.SectionName));

        // Logger (Singleton, mirrors Learning/Gamification pattern — no duplicate if already registered).
        services.AddSingleton<ILoggerManager, LoggerManager>();

        // Router (Singleton — pure, stateless mapping; safe to share).
        services.AddSingleton<IAiModelRouter, AiModelRouter>();

        // Named HttpClient — Claude.
        // BaseAddress is fixed to the Anthropic Messages API endpoint.
        // The per-call API key is injected at request time inside ClaudeProvider (never here).
        services.AddHttpClient(ClaudeProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.DefaultRequestHeaders.Add(
                "anthropic-version", "2023-06-01");
        });

        // Named HttpClient — OpenAI.
        services.AddHttpClient(OpenAiProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
        });

        // Provider adapters (Scoped — each request gets its own instance; HttpClient is pooled via factory).
        services.AddScoped<IAiProvider, ClaudeProvider>();
        services.AddScoped<IAiProvider, OpenAiProvider>();

        // Gateway facade (Scoped — resolves IAiProvider collection per request).
        services.AddScoped<IAiGateway, AiGateway>();

        // ── P3-02 Safety Layer ────────────────────────────────────────────────────

        // Safety check implementations (Scoped — depend on scoped IAiGateway).
        services.AddScoped<IToxicityCheck, ToxicityCheck>();
        services.AddScoped<IAgeAppropriatenessCheck, AgeAppropriatenessCheck>();
        services.AddScoped<IHallucinationCheck, HallucinationCheck>();

        // Safety event store — append-only write to ai.SafetyEvents (Scoped — depends on scoped AiDbContext).
        services.AddScoped<IAiSafetyEventStore, AiSafetyEventStore>();

        // P7-10-BE: Platform-aggregate read seam — platform-wide AI safety stats for the admin KPI dashboard.
        // Reads SafetyEvent table (OccurredAtUtc window). AI request volume is from AiUsageLogs (P7-11).
        // Scoped: depends on scoped AiDbContext.
        services.AddScoped<IPlatformAiSafetyStatsQuery, PlatformAiSafetyStatsQueryAdapter>();

        // P6-02-BE-5: Eval-result read seam — returns the latest offline eval run result from the
        // embedded safety-eval-results.json artifact (no DB, no migration; Option 1 from brief §C).
        // Scoped: mirrors IPlatformAiSafetyStatsQuery registration; depends on ILoggerManager (Singleton-safe).
        services.AddScoped<IAiSafetyEvalResultsQuery, AiSafetyEvalResultsQueryAdapter>();

        // P6-05-BE-2: AI-readiness probe seam — config-inspection only; no model call, no real key needed.
        // Scoped: mirrors IAiSafetyEvalResultsQuery registration. Host's AiGatewayHealthCheck injects
        // only this Shared.Contracts interface — never an Ai-module internal type (module isolation).
        services.AddScoped<IAiReadinessProbe, AiReadinessProbe>();

        // ── P7-11 AI-safety admin dashboard (read model) ─────────────────────────────
        // Scoped — depends on scoped AiDbContext; owns all EF read queries for the dashboard.
        services.AddScoped<IAiSafetyDashboardService, AiSafetyDashboardService>();

        // ── P7-11 AI usage/cost write path + admin read model ────────────────────────
        // AiUsageLogStore — append-only write to ai.AiUsageLogs.
        // Scoped: depends on scoped AiDbContext (resolved per-scope inside AiUsageRecorder's Task.Run).
        services.AddScoped<IAiUsageLogStore, AiUsageLogStore>();

        // AiUsageRecorder — fire-and-forget singleton. Holds only IServiceScopeFactory + ILoggerManager;
        // creates its own DI scope per background write so the caller's request scope is never touched.
        services.AddSingleton<IAiUsageRecorder, AiUsageRecorder>();

        // AiTutorUsageService — admin read model for the usage/cost dashboard.
        // Scoped: depends on scoped AiDbContext; owns all EF aggregation for the usage endpoint.
        services.AddScoped<IAiTutorUsageService, AiTutorUsageService>();

        // Safety Layer facade — the ONLY type that produces SafeAiResult for feature handlers.
        // AC1 (P3-02): no feature handler may call IAiGateway directly; only ISafetyLayer.
        services.AddScoped<ISafetyLayer, SafetyLayer>();

        // ── WI-B1: IGlobalSettingsProvider ──────────────────────────────────────
        // Bootstrap implementation reads from IConfiguration (AiHelper:Cache:* prefix).
        // Singleton — stateless config-bound; P10-12 swaps this impl without caller changes.
        // Registered in Infrastructure (not Host) so every Ai module consumer gets it via DI.
        // The Host also registers this for cross-module consumers — idempotent (same impl, last wins).
        services.AddSingleton<IGlobalSettingsProvider, BootstrapDefaultGlobalSettingsProvider>();

        // ── WI-B3: IAiResponseCache + AiResponseCacheRepository ─────────────────
        // Scoped — depends on Scoped AiDbContext; IDistributedCache is Singleton-compatible but
        // the Scoped lifetime is safer here (matches the request scope of the handlers).
        services.AddScoped<IAiResponseCache, AiResponseCacheRepository>();

        // ── WI-B5: IAiTutorRateLimiter — Redis/Null selector ────────────────────
        // Mirrors Gamification's Redis/Null pattern (AddGamificationCache in Gamification DI).
        // When IConnectionMultiplexer is present (Redis configured) → RedisAiRateLimiter.
        // When absent → fall back to in-process AiTutorRateLimiter (ConcurrentDictionary).
        // Both implement IAiTutorRateLimiter; Singleton so the counter persists per process.
        services.AddSingleton<IAiTutorRateLimiter>(sp =>
        {
            var mux    = sp.GetService<IConnectionMultiplexer>();
            var logger = sp.GetRequiredService<ILoggerManager>();
            if (mux is not null)
            {
                logger.LogInfo("IAiTutorRateLimiter: Redis available — using RedisAiRateLimiter (shared across instances).");
                return new RedisAiRateLimiter(mux, logger);
            }
            logger.LogInfo("IAiTutorRateLimiter: Redis absent — falling back to AiTutorRateLimiter (in-process only).");
            return new AiTutorRateLimiter();
        });

        return services;
    }
}
