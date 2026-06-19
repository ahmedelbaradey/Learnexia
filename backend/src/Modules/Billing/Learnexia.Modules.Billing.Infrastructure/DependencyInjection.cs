using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Infrastructure.Contracts;
using Learnexia.Modules.Billing.Infrastructure.Jobs;
using Learnexia.Modules.Billing.Infrastructure.Options;
using Learnexia.Modules.Billing.Infrastructure.Persistence;
using Learnexia.Modules.Billing.Infrastructure.Providers;
using Learnexia.Modules.Billing.Infrastructure.Queries;
using Learnexia.Modules.Billing.Infrastructure.Service;
using Learnexia.Modules.Billing.Infrastructure.Services;
using RefundService = Learnexia.Modules.Billing.Infrastructure.Services.RefundService;
using Learnexia.Shared.Contracts.Billing;
using Learnexia.Shared.Contracts.Parent;
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

        // ── BillingConcurrencyOptions — xmin OCC retry policy ──────────────────────────────────────
        // Bound from Billing:Concurrency in appsettings. Consumed by CreditLedgerService and
        // CreditSpendService (exponential back-off + jitter between concurrent-conflict retries).
        services.Configure<BillingConcurrencyOptions>(
            configuration.GetSection(BillingConcurrencyOptions.SectionName));

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

        // IBillingSubscriptionContract — real implementation (P10-05).
        // Replaces the ConfigDefaultSubscriptionContract stub.
        // Scoped: reads BillingDbContext (also Scoped) + IParentChildQuery (Scoped in Parent module).
        // IParentChildQuery is registered by the Parent module at Host startup.
        services.AddScoped<IBillingSubscriptionContract, RealBillingSubscriptionContract>();

        // BillingGrantJob — Hangfire job; Transient mirrors StreakSweepJob registration.
        // Creates its own inner scope per child via IServiceScopeFactory.
        services.AddTransient<BillingGrantJob>();

        // ── P10-06: Payment provider seam ───────────────────────────────────────────────
        // Config-driven selection: Billing:PaymentProvider:Provider = "Fake" (default) | "Paymob" (external).
        // Only FakePaymentProvider is built here; the real adapter is [EXTERNAL] — swap in later.
        // Scoped: aligns with the handler / BillingDbContext lifetime.
        var providerName = configuration["Billing:PaymentProvider:Provider"] ?? "Fake";
        if (string.Equals(providerName, "Fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IPaymentProvider, FakePaymentProvider>();
        }
        else
        {
            // TODO (EXTERNAL): register real PaymobPaymentProvider here when the adapter is built.
            // Until then, fall back to Fake so the application starts without secrets.
            services.AddScoped<IPaymentProvider, FakePaymentProvider>();
        }

        // ReconcilePaymentsJob — sweeps stale Initiated/Pending payments.
        services.AddTransient<ReconcilePaymentsJob>();

        // ── P10-07: Energy-pack purchase service (Option C seam) ────────────────────────────────
        // IEnergyPackService owns all EF/transaction/provider logic for pack purchases.
        // Application-layer handlers inject this seam and stay EF-free (Option C rule).
        // Scoped: depends on the scoped BillingDbContext + ICurrentUserService + IParentChildQuery.
        // IParentChildQuery is registered by the Parent module at Host startup.
        services.AddScoped<IEnergyPackService, EnergyPackService>();

        // ── P10-09: Refund + dunning service (Option C seam) ───────────────────────────────────
        // IRefundService owns all EF/transaction/ledger/provider logic for refunds and dunning.
        // Application-layer handlers inject this seam and stay EF-free (Option C rule).
        // Scoped: depends on the scoped BillingDbContext + IPaymentProvider + ICurrentUserService.
        // IServiceScopeFactory is used inside ProcessDunningRetriesAsync for per-subscription scopes
        // (mirrors BillingGrantJob pattern).
        services.AddScoped<IRefundService, RefundService>();

        // ── P10-08: Billing history query service (Option C seam) ──────────────────────────────
        // IBillingHistoryQueryService owns all EF query/projection/pagination logic.
        // Application-layer handlers inject this seam and stay EF-free (Option C rule).
        // Scoped: depends on the scoped BillingDbContext.
        services.AddScoped<IBillingHistoryQueryService, BillingHistoryQueryService>();

        // DunningRetryJob — Hangfire job; Transient mirrors BillingGrantJob registration.
        services.AddTransient<DunningRetryJob>();

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

        // ── Option C — new service seams (handlers stay EF-free) ─────────────────────────────────

        // ICreditLedgerService — owns all ledger write/read paths for the family wallet
        // (GrantFamilyWalletAsync, SpendAsync, ReconcileFamilyWalletAsync, GetEnergyStatusAsync).
        // CreditAccount is retired; migration-only artifact via CreditAccountMigrationService.
        // Scoped: depends on scoped BillingDbContext + ILoggerManager (Singleton — safe).
        services.AddScoped<ICreditLedgerService, CreditLedgerService>();

        // IWebhookEventService — owns all webhook idempotency + state flip logic.
        // Scoped: depends on scoped BillingDbContext + IEnergyPackService + IRefundService.
        services.AddScoped<IWebhookEventService, WebhookEventService>();

        // ISubscriptionCheckoutService — owns subscription checkout payment creation.
        // Scoped: depends on scoped BillingDbContext + IPaymentProvider + IGlobalSettingsProvider.
        services.AddScoped<ISubscriptionCheckoutService, SubscriptionCheckoutService>();

        // ISubscriptionService — owns cancel / downgrade / upgrade / read flows.
        // Scoped: depends on scoped BillingDbContext + IGlobalSettingsProvider + IPaymentProvider.
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        // IGlobalSettingService — owns GlobalSetting admin write + read flows.
        // IGlobalSettingValidationService — provides cross-key direct-DB reads for the validator.
        // Both implemented by GlobalSettingService (scoped singleton per scope).
        services.AddScoped<GlobalSettingService>();
        services.AddScoped<IGlobalSettingService>(sp => sp.GetRequiredService<GlobalSettingService>());
        services.AddScoped<IGlobalSettingValidationService>(sp => sp.GetRequiredService<GlobalSettingService>());

        // ── P10-13: Family energy wallet seams (Option C) ────────────────────────────────────────

        // ISeatQuery — P10-14-BE-3a: RealSeatQuery (TemporarySeatQuery deleted — NIT-c).
        // RealSeatQuery is backed by SeatReservation rows (SeatStatus.Active only).
        // Scoped: depends on scoped BillingDbContext + IGlobalSettingsProvider (Singleton — safe).
        services.AddScoped<ISeatQuery, RealSeatQuery>();

        // ISeatService — P10-14-BE-3: owns all seat state-machine, reservation, checkout,
        // and cancel logic. Application-layer handlers inject this seam and stay EF-free.
        // Scoped: depends on scoped BillingDbContext + IPaymentProvider + ICurrentUserService.
        services.AddScoped<ISeatService, SeatService>();

        // ISubscriptionSeatContract — P10-14-BE-4: cross-module seam allowing the Parent module
        // to reserve / release / check / activate seats without referencing any Billing types.
        // Implemented in Billing.Infrastructure; consumed via Shared.Contracts in the Parent module.
        // Scoped: delegates to ISeatService (scoped).
        services.AddScoped<ISubscriptionSeatContract, SubscriptionSeatContract>();

        // IFamilyEnergyAllocationService — owns the equal-split grant-deposit + allocation-row
        // upsert logic in an explicit transaction. Application handlers stay EF-free.
        // Scoped: depends on scoped BillingDbContext + ISeatQuery + ICurrentUserService.
        services.AddScoped<IFamilyEnergyAllocationService, FamilyEnergyAllocationService>();

        // IFamilyEnergyQueryService — owns all EF query/projection logic for the parent read path.
        // Application handlers stay EF-free (Option C).
        // Scoped: depends on scoped BillingDbContext.
        services.AddScoped<IFamilyEnergyQueryService, FamilyEnergyQueryService>();

        // ICreditAccountMigrationService — idempotent clean-cutover migration (P10-13-BE-9).
        // One-shot service (can be called from the BillingModule startup hook or an admin endpoint).
        // Scoped: depends on scoped BillingDbContext + IParentChildQuery + ICurrentUserService.
        services.AddScoped<ICreditAccountMigrationService, CreditAccountMigrationService>();

        // ── P10-15: Seat lifecycle services ─────────────────────────────────────────────────────

        // ISeatGraceService — P10-15-BE-2: opens/checks payment-failure seat grace windows.
        // Scoped: depends on scoped BillingDbContext + IGlobalSettingsProvider + ICurrentUserService.
        services.AddScoped<ISeatGraceService, SeatGraceService>();

        // ISeatSchedulingService — P10-15-BE-3: sets RemovalScheduledAt on SeatReservation rows.
        // Scoped: depends on scoped BillingDbContext + ICurrentUserService.
        services.AddScoped<ISeatSchedulingService, SeatSchedulingService>();

        // ISeatEnforcementService — P10-15-BE-4: applies enforcement (lock over-limit seats).
        // Scoped: depends on BillingDbContext (concrete — needs SaveChanges on enforcement scan).
        services.AddScoped<ISeatEnforcementService, SeatEnforcementService>();

        // SeatEnforcementJob — Hangfire job; Transient mirrors DunningRetryJob registration.
        services.AddTransient<SeatEnforcementJob>();

        // ISeatStateQuery (Shared.Contracts.Billing) — P10-15-BE-8: cross-module "is child seat active?" seam.
        // Used by CreditSpendService gate. Scoped: depends on scoped BillingDbContext.
        services.AddScoped<ISeatStateQuery, SeatStateQuery>();

        // ── P10-16: Family allocation redistribution service (Option C) ──────────────────────────────
        // IFamilyAllocationService — owns the transfer workflow: same-family guard, cap-at-remaining,
        // mid-cycle-INSERT, paired ledger writes, idempotency, explicit transaction.
        // Application handlers inject this seam and stay EF-free.
        // Scoped: depends on scoped BillingDbContext + ISeatStateQuery + ICurrentUserService.
        services.AddScoped<IFamilyAllocationService, FamilyAllocationService>();

        // ── P10-17: Purchased-energy refund services (Option C) ────────────────────────────────────
        // IAdminRefundQueryService — admin-privileged read: resolves family account from any payment
        // (not family-scoped). Application-layer admin handler stays EF-free.
        // Scoped: depends on scoped BillingDbContext.
        services.AddScoped<IAdminRefundQueryService, AdminRefundQueryService>();

        // ── P10-18: Parent-pause / child access control (Option C) ─────────────────────────────────

        // IPauseChildService — owns pause/unpause workflow: family-scope guard, idempotency,
        // state flip + ledger append in an explicit transaction. Zero energy/seat side-effects.
        // Application handlers inject this seam and stay EF-free.
        // Scoped: depends on scoped BillingDbContext + ICurrentUserService.
        services.AddScoped<IPauseChildService, PauseChildService>();

        // IChildAccessStateQuery (Shared.Contracts.Billing) — P10-18-BE-5: cross-module combined
        // access seam. Returns true ONLY when SeatState==Active AND ParentPauseState==Active.
        // Used by the Ai module and any other module that needs the unified access gate.
        // Scoped: depends on scoped BillingDbContext.
        services.AddScoped<IChildAccessStateQuery, ChildAccessStateQuery>();

        // ── P5-08-BE-4: Child energy usage read seam (Parent dashboard read) ─────────────────────
        // IChildEnergyUsageQuery — PURE READ. Reads ChildEnergyAllocation + FamilyEnergyAccount
        // + CreditTransaction with AsNoTracking(). Does NOT call GetBalanceAsync (which bootstraps
        // a ChildDailyUsage row — write-on-read). A child with no wallet/allocation → zeroed result.
        // Scoped: depends on scoped BillingDbContext.
        services.AddScoped<IChildEnergyUsageQuery, PostgresChildEnergyUsageQuery>();

        // P7-10-BE: Platform-aggregate read seam — platform-wide subscription stats for the admin KPI dashboard.
        // Reads Subscription table (active rows grouped by PlanCode). Revenue N/A in v1 (Fake provider).
        // Scoped: depends on scoped BillingDbContext.
        services.AddScoped<IPlatformSubscriptionStatsQuery, PlatformSubscriptionStatsQueryAdapter>();

        return services;
    }
}
