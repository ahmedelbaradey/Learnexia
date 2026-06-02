using AspNetCoreRateLimit;
using StackExchange.Redis;
using Hangfire;
using Hangfire.PostgreSql;
using Learnexia.Host.Extensions;
using Learnexia.Host.Middleware;
using Learnexia.Host.SystemConfiguration;
using Learnexia.Modules.Catalog.Api;
using Learnexia.Modules.Identity.Api;
using Learnexia.Modules.Learning.Api;
using Learnexia.Modules.Parent.Api;
using Learnexia.Modules.Notifications.Api;
using Learnexia.Modules.Gamification.Api;
using Learnexia.Shared.Kernel.Storage;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

// The codebase stamps DateTime.Now (Kind=Local) on entities/audit/seed (ported from the SQL Server
// original). Npgsql maps DateTime to 'timestamp with time zone', which rejects Local kinds. This switch
// restores the legacy behavior (accepts Local/Unspecified). Longer term, move timestamps to DateTime.UtcNow.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

// Optional, gitignored per-developer overrides (e.g. a private ConnectionStrings:Default pointing at a
// remote/shared DB). Never commit secrets to the tracked appsettings.{Environment}.json files; put them
// here instead. Matches the `appsettings.*.local.json` entry in .gitignore.
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json",
    optional: true,
    reloadOnChange: true);

builder.Services.AddSwaggerService();

// Cross-cutting host services (ported from backend/src/apis/Main)
builder.Services.ConfigureCors(builder.Configuration);
builder.Services.ConfigureRateLimitingOptions(builder.Configuration);
builder.Services.ConfigureIISIntegration();
builder.Services.ConfigureVersioning();
builder.Services.ConfigureResponseCaching();
builder.Services.AddMemoryCache();
builder.Services.ConfigureLocalization();
builder.Services.ConfigureForwardedHeaders();
builder.Services.AddHttpContextAccessor();

// Platform-wide MinIO object storage (relocated from Identity to Shared.Kernel — a shared capability for
// ANY file upload). Registered ONCE here at the Host so every module can inject IStorageService.
builder.Services.AddMinIODependencies(builder.Configuration);

// IDistributedCache backing for sessions / token cache.
// When a Redis endpoint is configured (compose injects ConnectionStrings__Redis=redis:6379), back the
// distributed cache with Redis so sessions/token cache survive restarts and span multiple instances.
// When absent (local dev / tests), fall back to in-memory so the app stays runnable without Redis.
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "Learnexia:";
    });

    // Register IConnectionMultiplexer for direct Redis ops (sorted-set leaderboard, SETNX, etc.).
    // Conditional on the same Redis connection string gate as AddStackExchangeRedisCache above.
    // Not registered when Redis is absent — modules must accept IConnectionMultiplexer? (nullable).
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
        ConnectionMultiplexer.Connect(redisConnectionString));
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Health checks (P1-07-BE-2) — readiness probe dependencies.
// - "database": Npgsql check against ConnectionStrings:Default (Unhealthy when the DB is down → 503).
// - "redis": only registered when ConnectionStrings:Redis is set; failureStatus = Degraded so a missing
//   or down Redis does NOT fail the readiness probe (Redis is optional locally, mirroring the cache wiring
//   above). When no Redis connection string is configured the check is skipped entirely.
var defaultConnectionString = builder.Configuration.GetConnectionString("Default");
var healthChecks = builder.Services.AddHealthChecks();
if (!string.IsNullOrWhiteSpace(defaultConnectionString))
{
    healthChecks.AddNpgSql(defaultConnectionString, name: "database", tags: ["ready"]);
}
if (!string.IsNullOrWhiteSpace(redisConnectionString))
{
    healthChecks.AddRedis(redisConnectionString, name: "redis", failureStatus: HealthStatus.Degraded, tags: ["ready"]);
}

// Background-jobs host (P1-07-BE-5) — Hangfire with PostgreSQL storage.
// This registers the scheduler + server only; NO jobs are implemented in this story. It is the home for
// later Phase 4 (streaks/leagues) and Phase 5 (report) recurring/background jobs. Hangfire bootstraps its
// own `hangfire` schema tables in the existing Learnexia database via its storage initializer (not an EF
// migration). The dashboard is gated to Development only (see the app section below).
if (!string.IsNullOrWhiteSpace(defaultConnectionString))
{
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(defaultConnectionString)));
    builder.Services.AddHangfireServer();
}

// Modules (each wires its own Application + Infrastructure + JWT auth + controllers application part)
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddLearningModule(builder.Configuration);
builder.Services.AddParentModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);
builder.Services.AddGamificationModule(builder.Configuration);

// Single, cross-module MediatR registration spanning every module's Application assembly + the
// IsolatedNotificationPublisher (ADR 0002 §4). Must come AFTER the modules register their validators /
// AutoMapper / ValidationBehavior. Enables cross-module IPublisher.Publish fan-out (FR-GM-7).
builder.Services.AddCrossModuleMediatR();

// Validation error shaping (422 with BaseResponse) — mirrors backend Main.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
    options.InvalidModelStateResponseFactory = ValidationErrorResponseFactory.CreateResponse;
});

NewtonsoftJsonPatchInputFormatter GetJsonPatchInputFormatter() =>
    new ServiceCollection().AddLogging().AddMvc().AddNewtonsoftJson().Services
        .BuildServiceProvider().GetRequiredService<IOptions<MvcOptions>>().Value
        .InputFormatters.OfType<NewtonsoftJsonPatchInputFormatter>().First();

builder.Services.AddControllers(config =>
{
    config.RespectBrowserAcceptHeader = true;
    config.ReturnHttpNotAcceptable = true;
    config.InputFormatters.Insert(0, GetJsonPatchInputFormatter());
    config.CacheProfiles.Add("120SecondsDuration", new CacheProfile { Duration = 120 });
}).AddXmlDataContractSerializerFormatters();

// Host-owned system configuration
builder.Services.Configure<SystemConfigurationOptions>(
    builder.Configuration.GetSection(SystemConfigurationOptions.SectionName));

var app = builder.Build();

app.UseCors("CorsPolicy");

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v2/swagger.json", "Learnexia API v2");
    options.DisplayRequestDuration();
});

var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>();
app.UseRequestLocalization(localizationOptions.Value);

app.UseHsts();
app.UseStaticFiles();
app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.All });

// Apply pending migrations per module + seed (all idempotent). Each module owns its own DbContext and
// exposes a Host-callable hook through its Api entry point — the Host never references module Infrastructure
// directly (module isolation). Identity's SeedAsync also migrates before seeding; Catalog and Notifications
// migrate via InitializeAsync. Modules are independent, so ordering is not significant.
using (var scope = app.Services.CreateScope())
{
    await IdentityModule.SeedAsync(scope.ServiceProvider);
    await CatalogModule.InitializeAsync(scope.ServiceProvider);
    await LearningModule.InitializeAsync(scope.ServiceProvider);
    await ParentModule.InitializeAsync(scope.ServiceProvider);
    await NotificationsModule.InitializeAsync(scope.ServiceProvider);
    await GamificationModule.InitializeAsync(scope.ServiceProvider);
}

// Health probes (P1-07-BE-2) — mapped BEFORE auth, rate limiting, and error/auth-logging middleware so
// container/orchestrator probes are never blocked by 401/429. Both endpoints are anonymous.
// - /health/live  : liveness — no dependency checks (Predicate = _ => false), always 200 if the process is up.
// - /health       : readiness — runs the registered checks (DB hard-fail → 503; Redis degraded-tolerant).
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
}).AllowAnonymous();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => true
}).AllowAnonymous();

app.UseMiddleware<ErrorHandlerMiddleWare>();
app.UseMiddleware<AuthorizationLoggingMiddleware>();
app.UseIpRateLimiting();
app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard (P1-07-BE-5) — DEVELOPMENT ONLY. Outside Development the middleware is not
// registered at all, so /hangfire returns 404 (never an anonymous dashboard in staging/production).
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

app.MapControllers();

// Module endpoints (minimal APIs for not-yet-controllerized modules)
app.MapCatalogModule();
app.MapLearningModule();
app.MapParentModule();
app.MapNotificationsModule();
app.MapGamificationModule();

// Host-owned endpoints
app.MapSystemConfiguration();

app.Run();
