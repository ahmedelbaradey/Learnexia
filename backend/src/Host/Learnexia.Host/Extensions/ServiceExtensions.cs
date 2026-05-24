using System.Globalization;
using Asp.Versioning;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.OpenApi.Models;
namespace Learnexia.Host.Extensions;

public static class ServiceExtensions
{
    public static void ConfigureCors(this IServiceCollection services, IConfiguration configuration) => services.AddCors(options =>
    {
        var urls = (configuration.GetValue<string>("AllowedOrigins") ?? "*").Split(',', StringSplitOptions.RemoveEmptyEntries);
        options.AddPolicy("CorsPolicy", builder =>
            builder.WithOrigins(urls)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .SetPreflightMaxAge(TimeSpan.FromMinutes(10))
                .WithExposedHeaders("X-Pagination"));
    });

    public static void ConfigureIISIntegration(this IServiceCollection services) => services.Configure<IISOptions>(_ => { });

    public static void ConfigureResponseCaching(this IServiceCollection services) => services.AddResponseCaching();

    public static void ConfigureRateLimitingOptions(this IServiceCollection services)
    {
        var rateLimitRules = new List<RateLimitRule>
        {
            new() { Endpoint = "*", Limit = 200, Period = "1m" },
            // P1-13b BE-1: per-endpoint abuse/DoS ceiling on the anonymous auth endpoints (100 req/s per IP).
            // Endpoint rules use the lowercased "{verb}:{path}" form and require EnableEndpointRateLimiting.
            new() { Endpoint = "post:/api/users/authentication/sign-in", Limit = 100, Period = "1s" },
            new() { Endpoint = "post:/api/users/authentication/register-parent", Limit = 100, Period = "1s" },
            new() { Endpoint = "post:/api/users/authentication/google-signin", Limit = 100, Period = "1s" },
            new() { Endpoint = "post:/api/users/authentication/forgot-password", Limit = 100, Period = "1s" },
            new() { Endpoint = "post:/api/users/authentication/reset-password", Limit = 100, Period = "1s" },
            // P2-12: tight limit on password-change (brute-force / oracle hardening). 5 attempts per 15 min per IP.
            new() { Endpoint = "post:/api/users/account/changepassword", Limit = 5, Period = "15m" },
        };
        services.Configure<IpRateLimitOptions>(opt =>
        {
            // Required so the "{verb}:{path}" rules above are counted per-endpoint (not folded into the global "*").
            opt.EnableEndpointRateLimiting = true;
            opt.GeneralRules = rateLimitRules;
            // Health probes must never be throttled — container/orchestrator probes hit them repeatedly.
            opt.EndpointWhitelist = new List<string> { "get:/health", "get:/health/live" };
        });
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
    }

    public static void ConfigureLocalization(this IServiceCollection services)
    {
        services.AddLocalization(opt => opt.ResourcesPath = "");

        services.Configure<RequestLocalizationOptions>(opt =>
        {
            var locales = new List<CultureInfo> { new("en-US"), new("ar-EG") };
            opt.DefaultRequestCulture = new RequestCulture("ar-EG");
            opt.SupportedCultures = locales;
            opt.SupportedUICultures = locales;
        });
    }

    public static void ConfigureVersioning(this IServiceCollection services)
    {
        services.AddApiVersioning(opt =>
        {
            opt.ReportApiVersions = true;
            opt.AssumeDefaultVersionWhenUnspecified = true;
            opt.DefaultApiVersion = new ApiVersion(2, 0);
            opt.ApiVersionReader = new HeaderApiVersionReader("api-version");
        }).EnableApiVersionBinding().AddMvc();
    }

    public static IServiceCollection AddSwaggerService(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(s =>
        {
            s.SwaggerDoc("v2", new OpenApiInfo
            {
                Title = "Learnexia Identity API",
                Version = "v2",
                Description = "Learnexia Identity API by Learnexia",
                TermsOfService = new Uri("https://example.com/terms"),
                Contact = new OpenApiContact
                {
                    Name = "Learnexia",
                    Email = "info@Learnexia.com",
                    Url = new Uri("https://linkedin.com/ahmedelbaradey"),
                },
                License = new OpenApiLicense
                {
                    Name = "Learnexia Identity API LICX",
                    Url = new Uri("https://example.com/license"),
                },
            });

            s.EnableAnnotations();

            s.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
            {
                Description = "Jwt Authentication header using the Bearer scheme (....)",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = JwtBearerDefaults.AuthenticationScheme,
            });

            s.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = JwtBearerDefaults.AuthenticationScheme,
                        }
                    },
                    Array.Empty<string>()
                },
            });
        });

        return services;
    }

    public static void ConfigureForwardedHeaders(this IServiceCollection services) =>
        services.Configure<ForwardedHeadersOptions>(options => options.ForwardedHeaders = ForwardedHeaders.All);
}
