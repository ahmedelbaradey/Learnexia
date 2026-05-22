using Learnexia.Modules.Catalog.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Api;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;
using Xunit;

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
            ReplaceDbContext<CatalogDbContext>(services, connectionString, "catalog");
            ReplaceDbContext<NotificationsDbContext>(services, connectionString, "notifications");

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

        var catalogDb = sp.GetRequiredService<CatalogDbContext>();
        await catalogDb.Database.MigrateAsync();

        // Notifications: 20260521111622_InitialNotifications migration creates the notifications schema/tables.
        var notificationsDb = sp.GetRequiredService<NotificationsDbContext>();
        await notificationsDb.Database.MigrateAsync();

        // Seed roles + superadmin (idempotent).
        await IdentityModule.SeedAsync(sp);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.StopAsync();
    }

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
