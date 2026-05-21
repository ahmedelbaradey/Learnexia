using AspNetCoreRateLimit;
using Learnexia.Host.Extensions;
using Learnexia.Host.Middleware;
using Learnexia.Host.SystemConfiguration;
using Learnexia.Modules.Catalog.Api;
using Learnexia.Modules.Identity.Api;
using Learnexia.Modules.Notifications.Api;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Options;

// The codebase stamps DateTime.Now (Kind=Local) on entities/audit/seed (ported from the SQL Server
// original). Npgsql maps DateTime to 'timestamp with time zone', which rejects Local kinds. This switch
// restores the legacy behavior (accepts Local/Unspecified). Longer term, move timestamps to DateTime.UtcNow.
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSwaggerService();

// Cross-cutting host services (ported from backend/src/apis/Main)
builder.Services.ConfigureCors(builder.Configuration);
builder.Services.ConfigureRateLimitingOptions();
builder.Services.ConfigureIISIntegration();
builder.Services.ConfigureVersioning();
builder.Services.ConfigureResponseCaching();
builder.Services.AddMemoryCache();
builder.Services.ConfigureLocalization();
builder.Services.ConfigureForwardedHeaders();
builder.Services.AddHttpContextAccessor();

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
}
else
{
    builder.Services.AddDistributedMemoryCache();
}

// Modules (each wires its own Application + Infrastructure + JWT auth + controllers application part)
builder.Services.AddIdentityModule(builder.Configuration);
builder.Services.AddCatalogModule(builder.Configuration);
builder.Services.AddNotificationsModule(builder.Configuration);

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

// Seed roles + users (idempotent). Identity owns the seed; Host only invokes the module hook.
using (var scope = app.Services.CreateScope())
{
    await IdentityModule.SeedAsync(scope.ServiceProvider);
}

app.UseMiddleware<ErrorHandlerMiddleWare>();
app.UseMiddleware<AuthorizationLoggingMiddleware>();
app.UseIpRateLimiting();
app.UseResponseCaching();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Module endpoints (minimal APIs for not-yet-controllerized modules)
app.MapCatalogModule();
app.MapNotificationsModule();

// Host-owned endpoints
app.MapSystemConfiguration();

app.Run();
