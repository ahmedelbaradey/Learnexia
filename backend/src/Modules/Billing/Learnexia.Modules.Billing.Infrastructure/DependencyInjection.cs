using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Infrastructure.Contracts;
using Learnexia.Modules.Billing.Infrastructure.Jobs;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Service;
using Learnexia.Modules.Billing.Infrastructure.Services;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Learnexia.Shared.Kernel.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // BillingDbContext — Npgsql/PostgreSQL, schema = "billing", migrations history in that schema.
        // Also hosts platform.GlobalSettings (separate schema, same DbContext — no new module needed).
        // Mirrors AiDbContext registration.
        services.AddDbContext<BillingDbContext>(options =>
            options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
                   .UseNpgsql(
                       configuration.GetConnectionString("Default"),
                       builder => builder
                           .MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema)
                           .MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName)));

        // Register IBillingDbContext → resolves to BillingDbContext (the scoped instance above).
        services.AddScoped<IBillingDbContext>(sp => sp.GetRequiredService<BillingDbContext>());

        // Logger (Singleton, mirrors Ai/Gamification pattern).
        services.AddSingleton<ILoggerManager, LoggerManager>();

        // ICurrentUserService — per-request user context (reads HttpContext).
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // ICreditSpendService — the cross-module seam consumed by the Ai module (W2/P10-03).
        // Scoped: depends on the scoped BillingDbContext + ICurrentUserService.
        // W2b: also injects IGlobalSettingsProvider (Singleton) + ISystemClock (Singleton)
        // for the atomic DailyUsed increment + GetBalanceAsync daily-cap population.
        services.AddScoped<ICreditSpendService, CreditSpendService>();

        // ISystemClock — stateless singleton, deterministic time seam for daily-cap and grant jobs.
        services.AddSingleton<ISystemClock, SystemClock>();

        // IBillingSubscriptionContract — stub returns all children as Free tier until P10-05 lands.
        // Scoped: reads BillingDbContext (also Scoped).
        services.AddScoped<IBillingSubscriptionContract, ConfigDefaultSubscriptionContract>();

        // BillingGrantJob — Hangfire job; Transient mirrors StreakSweepJob registration.
        // Creates its own inner scope per child via IServiceScopeFactory.
        services.AddTransient<BillingGrantJob>();

        // ── P10-12: DB-backed GlobalSettings store ───────────────────────────────────────────
        // DbBackedGlobalSettingsProvider implements both IGlobalSettingsProvider (the lean interface
        // — signature UNCHANGED, DRIFT-1 LOCKED) and ISettingsCacheInvalidator (internal seam).
        // Registered as Singleton: the in-memory ConcurrentDictionary must outlive individual
        // HTTP scopes; DB reads use IServiceScopeFactory to create a fresh scope per miss.
        // Overrides the BootstrapDefaultGlobalSettingsProvider registered in the Host + Ai.Infrastructure —
        // last registration wins in the default ASP.NET Core DI container.
        services.AddSingleton<DbBackedGlobalSettingsProvider>();
        services.AddSingleton<IGlobalSettingsProvider>(sp =>
            sp.GetRequiredService<DbBackedGlobalSettingsProvider>());
        services.AddSingleton<ISettingsCacheInvalidator>(sp =>
            sp.GetRequiredService<DbBackedGlobalSettingsProvider>());

        // Startup warm-up: bulk-loads all managed settings into the in-memory cache so the
        // first real request does not pay a per-key DB round-trip.
        services.AddHostedService<GlobalSettingsWarmupService>();

        // Audit is handled via AdminActionPerformedEvent published by the command handler after
        // commit (mirrors Gamification/Learning admin handlers). No module-local IAuditLogWriter
        // registration is needed — the cross-module Moderation AuditLogEventHandler persists the row.

        return services;
    }
}
