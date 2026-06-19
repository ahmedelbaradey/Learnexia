using Learnexia.Modules.Ai.Application;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.Safety;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Modules.Ai.Domain.Safety;
using Learnexia.Modules.Ai.Infrastructure.Cache;
using Learnexia.Modules.Ai.Infrastructure.Gateway;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Modules.Ai.Infrastructure.Providers;
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
        // Reads SafetyEvent table (OccurredAtUtc window). AI request volume N/A (P7-11 AiUsageLogs not yet built).
        // Scoped: depends on scoped AiDbContext.
        services.AddScoped<IPlatformAiSafetyStatsQuery, PlatformAiSafetyStatsQueryAdapter>();

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
