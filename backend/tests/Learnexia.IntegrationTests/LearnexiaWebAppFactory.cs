using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Api;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.IntegrationTests;

/// <summary>
/// Shared WebApplicationFactory fixture backed by a Testcontainers PostgreSQL container.
/// The container starts once per test collection and is torn down after all tests run.
/// </summary>
public sealed class LearnexiaWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .WithDatabase("Learnexia")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .Build();

    public async Task InitializeAsync()
    {
        // Start the Postgres container *before* the host is built.
        await _postgres.StartAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Npgsql legacy timestamp switch must be set before any host code runs.
        AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Override the connection string for all three DbContexts to point at the container.
            var connectionString = _postgres.GetConnectionString();

            ReplaceDbContext<IdentityModuleDbContext>(services, connectionString, "identity");
            ReplaceDbContext<NotificationsDbContext>(services, connectionString, "notifications");
            ReplaceDbContext<LearningDbContext>(services, connectionString, "learning");
            ReplaceDbContext<ParentDbContext>(services, connectionString, "parent");
            ReplaceDbContext<GamificationDbContext>(services, connectionString, "gamification");

            // Testing-host only: neutralise the IP rate limiter so the combined integration suite
            // (~250+ requests in well under a minute) never trips the production 200 req/min cap and
            // returns spurious 429s. This Configure<> runs *after* the host's ConfigureRateLimitingOptions,
            // so it wins for the Testing host without touching production behaviour in Program.cs.
            // The production rule (Endpoint "*", Limit 200, Period "1m") is unchanged outside tests.
            services.Configure<IpRateLimitOptions>(opt =>
            {
                opt.EnableEndpointRateLimiting = false;
                opt.GeneralRules = new List<RateLimitRule>
                {
                    new() { Endpoint = "*", Limit = int.MaxValue, Period = "1m" }
                };
            });

            // P4-03 clock seam: replace the production SystemClock singleton with our TestClock so
            // StreakDayCalculator.Today and StreakSweepJob use the settable clock in tests. The
            // TestClock defaults to DateTime.UtcNow unless a test calls factory.TestClock.SetUtcNow().
            services.RemoveAll<ISystemClock>();
            services.AddSingleton<ISystemClock>(TestClock);

            // P4-09 push sender seam: replace the real IPushSender (ExpoPushSender or LogPushSender)
            // with a test recorder so integration tests can assert push dispatch without calling Expo.
            services.RemoveAll<IPushSender>();
            services.AddScoped<IPushSender>(_ => PushSender);
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            // Ensure JwtSettings are present so JWT token generation does not throw.
            // The appsettings.json in the Host project provides them, but we also set them
            // here as a safety net.
        });
    }

    /// <summary>
    /// Apply migrations and seed all three modules' schemas after the host is built.
    /// </summary>
    public async Task ApplyMigrationsAndSeedAsync()
    {
        using var scope = Services.CreateScope();
        var sp = scope.ServiceProvider;

        var identityDb = sp.GetRequiredService<IdentityModuleDbContext>();
        await identityDb.Database.MigrateAsync();

        // Notifications: 20260521111622_InitialNotifications migration creates the notifications schema/tables.
        var notificationsDb = sp.GetRequiredService<NotificationsDbContext>();
        await notificationsDb.Database.MigrateAsync();

        // Learning: InitialLearning migration creates the learning schema/tables.
        var learningDb = sp.GetRequiredService<LearningDbContext>();
        await learningDb.Database.MigrateAsync();

        // Parent: InitialParent migration creates the parent schema/tables (P2-12).
        var parentDb = sp.GetRequiredService<ParentDbContext>();
        await parentDb.Database.MigrateAsync();

        // Gamification: InitGamification migration creates gamification schema/tables (P4-02).
        var gamificationDb = sp.GetRequiredService<GamificationDbContext>();
        await gamificationDb.Database.MigrateAsync();

        // Seed roles + superadmin (idempotent).
        await IdentityModule.SeedAsync(sp);

        // The IdentitySeeder only seeds the legacy dev accounts (superadmin / basicuser) when
        // IsDevelopment() is true. In the Testing environment (used by WebApplicationFactory) those
        // accounts are absent, which makes all tests that depend on them cascade-fail.
        // Explicitly seed the dev accounts here so the test suite mirrors the expected dev state.
        var userManager = sp.GetRequiredService<UserManager<User>>();
        var roleManager = sp.GetRequiredService<RoleManager<Role>>();
        await UserSeeder.SeedBasicUserAsync(userManager, roleManager);
        await UserSeeder.SeedSuperAdminAsync(userManager, roleManager);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
    }

    // -------------------------------------------------------------------------
    // Test clock seam (P4-03): lets streak and sweep tests control "today".
    // Access via factory.TestClock and set TestClock.UtcNow before the action.
    // The TestClock replaces ISystemClock (Singleton) in ConfigureServices so
    // StreakDayCalculator.Today + StreakSweepJob both read from it.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Settable <see cref="ISystemClock"/> for deterministic streak/sweep tests.
    /// Defaults to the real <see cref="DateTime.UtcNow"/> so tests that don't touch it are unaffected.
    /// </summary>
    public sealed class TestSystemClock : ISystemClock
    {
        private DateTime? _override;
        public void SetUtcNow(DateTime utcNow) => _override = utcNow;
        public void Reset() => _override = null;
        public DateTime UtcNow => _override ?? DateTime.UtcNow;
    }

    public TestSystemClock TestClock { get; } = new TestSystemClock();

    // -------------------------------------------------------------------------
    // P4-09 push sender seam: TestPushSender records send attempts so integration
    // tests can assert dispatch without calling the real Expo API.
    // -------------------------------------------------------------------------

    /// <summary>
    /// Test double for <see cref="IPushSender"/>. Records every <c>SendAsync</c> call
    /// (tokens, title, body, dataJson) so integration tests can assert push dispatch.
    /// Call <see cref="Reset"/> before each test to clear recorded sends.
    /// </summary>
    public sealed class TestPushSenderImpl : IPushSender
    {
        public readonly List<(IReadOnlyList<string> Tokens, string Title, string Body, string? DataJson)> Calls = new();

        public Task<PushSendResult> SendAsync(
            IReadOnlyList<string> expoPushTokens,
            string title,
            string body,
            string? dataJson,
            CancellationToken ct = default)
        {
            Calls.Add((expoPushTokens, title, body, dataJson));
            return Task.FromResult(new PushSendResult(Sent: expoPushTokens.Count, Failed: 0, InvalidTokens: []));
        }

        public void Reset() => Calls.Clear();
        public int CallCount => Calls.Count;
    }

    /// <summary>
    /// Shared singleton instance of the test push sender. Registered as Scoped in ConfigureServices
    /// but the same object is referenced so tests can inspect it. Reset before each test.
    /// </summary>
    public TestPushSenderImpl PushSender { get; } = new TestPushSenderImpl();

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static void ReplaceDbContext<TContext>(
        IServiceCollection services,
        string connectionString,
        string schema) where TContext : DbContext
    {
        // Remove the existing registration that points at localhost.
        services.RemoveAll<DbContextOptions<TContext>>();
        services.RemoveAll<TContext>();

        // Each module's DbContext applies its own warning configuration in OnConfiguring
        // (e.g. PendingModelChangesWarning ignore), so the test override does not need to
        // re-specify it — it relies on the context-level config like the other modules.
        services.AddDbContext<TContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql
                    .MigrationsHistoryTable("__EFMigrationsHistory", schema)
                    .MigrationsAssembly(typeof(TContext).Assembly.FullName)));
    }
}
