using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Modules.Identity.Infrastructure.Services;
using Learnexia.Modules.Identity.Infrastructure.Services.Sessions;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Learnexia.Shared.Kernel.Logging;
using Learnexia.Modules.Identity.Infrastructure.Behaviors;
using MediatR;


namespace Learnexia.Modules.Identity.Infrastructure;

public static class DependencyInjection
{    
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAutoMapper(cfg => cfg.AddMaps(Assembly.GetExecutingAssembly()));
        services.AddLoggerServices(configuration);
        services.AddDbContext(configuration);
        services.AddIdentityService(configuration);

        // P1-12 BE-4: MinIO object storage is now registered ONCE at the Host (relocated to Shared.Kernel
        // as a platform-wide capability — see Host Program.cs AddMinIODependencies). Modules inject
        // IStorageService directly; no module-local registration here.

        // Unit-of-Work behavior (ADR 0001 §2 + ADR 0002 §2): commit once per ICommand<>, then dispatch
        // the aggregates' domain events AFTER commit. Registered here in Infrastructure (not Application)
        // because it injects the concrete IdentityModuleDbContext, which Application cannot reference.
        // Registered AFTER ValidationBehavior (added in AddIdentityApplication, which runs before this) so
        // validation rejects bad input before a transaction opens. The IDomainEventDispatcher it depends on
        // is registered at the Host (AddCrossModuleMediatR), alongside the unified IPublisher it wraps.
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkBehavior<,>));



        var sessionSettings = configuration.GetSection("SessionSettings").Get<SessionSettings>() ?? new SessionSettings();
        services.AddSingleton(sessionSettings);

        services.AddHttpContextAccessor();

        services.AddScoped<ILoggerManager, LoggerManager>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISessionManagementService, SessionManagementService>();
        services.AddScoped<IIdentityServiceManager, IdentityServiceManager>();
        services.AddScoped<ILinkParentStudentService, LinkParentStudentService>();

        // Google social sign-in (P1-12 BE-5). Bind the "GoogleAuth" section (ClientId is the OAuth
        // audience; supplied via GoogleAuth__ClientId env in real environments) and register the
        // SDK-backed ID-token validator. Singleton: stateless, no scoped dependencies.
        services.Configure<Application.Configurations.GoogleAuthSettings>(configuration.GetSection("GoogleAuth"));
        services.AddSingleton<IGoogleTokenValidator, Services.GoogleTokenValidator>();

        // Anti-automation CAPTCHA on register (P1-13 BE-4). Bind the "Captcha" section (Enabled +
        // SecretKey; the secret is supplied via Captcha__SecretKey env in real environments) and
        // register the Turnstile verifier behind a typed HttpClient. Config-gated: when
        // Captcha:Enabled=false (the committed default) VerifyAsync is a no-op that returns true,
        // so dev/tests register with no token; when enabled it fails closed.
        services.Configure<Application.Configurations.CaptchaSettings>(configuration.GetSection("Captcha"));
        services.AddHttpClient<ICaptchaVerifier, Services.TurnstileCaptchaVerifier>();
        // Fail fast on a misconfigured CAPTCHA: if it's enabled but no secret is supplied, every
        // register would silently fail-closed. Reject at startup (mirrors GuardJwtSecret).
        var captchaSettings = configuration.GetSection("Captcha").Get<Application.Configurations.CaptchaSettings>();
        if (captchaSettings is { Enabled: true } && string.IsNullOrWhiteSpace(captchaSettings.SecretKey))
            throw new InvalidOperationException("Captcha:Enabled is true but Captcha:SecretKey is not configured. Set Captcha__SecretKey (env) or disable Captcha.");

        // P1-04: family-scope resource authorization handler (consumed by P1-05). Scoped because it
        // injects the scoped IdentityModuleDbContext. Minimal single-line addition — see merge note.
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, Authorization.FamilyScopeAuthorizationHandler>();

        // Cross-module seams (stubs until the real adapters are provided)
        services.AddScoped<Learnexia.Shared.Contracts.Notifications.IUserNotificationService, Services.Stubs.NoOpUserNotificationService>();
        services.AddScoped<Learnexia.Shared.Contracts.Storage.IFilePreviewUrlProvider, Services.Stubs.NoOpFilePreviewUrlProvider>();

        // Identity-side implementation of the IUserLookup seam: lets Notifications (welcome-email)
        // and password-reset resolve a user's email by id. Real implementation, not a stub.
        services.AddScoped<Learnexia.Shared.Contracts.Identity.IUserLookup, Services.UserLookup>();

        return services;
    }


    public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
      services.AddDbContext<IdentityModuleDbContext>(options => options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
      .UseNpgsql(configuration.GetConnectionString("default"),  builder => builder.MigrationsHistoryTable("__EFMigrationsHistory", IdentityModuleDbContext.Schema).MigrationsAssembly(typeof(IdentityModuleDbContext).Assembly.FullName)));
    }

    public static IServiceCollection AddLoggerServices(this IServiceCollection services, IConfiguration configuration)
    {
            services.AddSingleton<ILoggerManager, LoggerManager>();
            return services;
    }
    public static IServiceCollection AddIdentityService(this IServiceCollection services, IConfiguration configuration)
    {

            services.AddIdentityCore<User>(opt =>
            {
                //Some Options for Loggin And Password & etc....
                // Password settings.
                opt.Password.RequireDigit = true;
                opt.Password.RequireLowercase = true;
                opt.Password.RequireNonAlphanumeric = true;
                opt.Password.RequireUppercase = true;
                opt.Password.RequiredLength = 6;
                opt.Password.RequiredUniqueChars = 1;
                // Lockout settings.
                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
                opt.Lockout.MaxFailedAccessAttempts = 5;
                opt.Lockout.AllowedForNewUsers = true;
                // User settings.
                opt.User.AllowedUserNameCharacters =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
                opt.User.RequireUniqueEmail = true;
                opt.SignIn.RequireConfirmedEmail = false;
            })
            .AddRoles<Role>()
            .AddSignInManager()
            .AddEntityFrameworkStores<IdentityModuleDbContext>()
            .AddDefaultTokenProviders();
            //this is the first code for check the username and password
            //Binding between jwtSettings Json & JwtSettings Class
            var jwtSettings = configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();

            // Fail-fast guard (security finding #1): the committed default signing secret is publicly
            // known, so anyone could forge tokens (incl. Admin/SuperAdmin) once a token-issuing endpoint
            // ships. In Production/Staging the secret MUST be supplied out-of-band — set the
            // `JwtSettings__Secret` environment variable (or a secret store) to a strong value. We reject
            // an empty value or the known CHANGE_ME default there. The default is tolerated only in
            // Development/Testing so local dev and the WebApplicationFactory integration tests keep working.
            GuardJwtSecret(jwtSettings, configuration);

            services.AddSingleton(jwtSettings);

           
    
            //Jwt Authentication settings 
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            }).AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = false;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = jwtSettings.ValidateIssure,
                    ValidIssuers = new[] { jwtSettings.Issure },
                    ValidateIssuerSigningKey = jwtSettings.ValidateIssureSigningKey,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSettings.Secret)),
                    ValidateAudience = jwtSettings.validateAudience,
                    ValidAudience = jwtSettings.Audience,
                    ValidateLifetime = jwtSettings.ValidateLifeTime
                };
            });
            services.AddAuthorization(options =>
            {
                foreach (var module in Claims.GenerateModules())
                {
                    foreach (var permission in Claims.GeneratePermissions())
                    {
                    options.AddPolicy(permission, policy => { policy.RequireClaim(CustomClaimTypes.Permission, permission); });
                    }
                }

                // Admin-only gate for observability/admin-lookup endpoints (e.g. notifications
                // by-recipient lookup). Roles are emitted as ClaimTypes.Role in the JWT
                // (AuthenticationIdentityService.GetClaims) verbatim from the seeded role Name,
                // which RoleSeeder now standardizes to the enum's PascalCase. RequireRole compares
                // case-sensitively (ordinal), so the policy must use the same PascalCase enum names
                // (Roles.Admin/Roles.SuperAdmin) — NOT RoleHelper's lower-case constants — or every
                // authenticated admin would 403. The seeded superadmin holds both Admin and SuperAdmin.
                options.AddPolicy(Learnexia.Shared.Kernel.Abstractions.AuthorizationPolicies.AdminOnly, policy =>
                    policy.RequireRole(Roles.Admin.ToString(), Roles.SuperAdmin.ToString()));
            });
            return services;
    }

    // The publicly-committed placeholder secret. Permitted only in Development/Testing.
    private const string DefaultJwtSecret = "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789";

    /// <summary>
    /// Rejects the default/empty JWT signing secret at startup when running in Production or Staging,
    /// so a deployment can never ship signing tokens with the publicly-known key (security finding #1).
    /// The default is allowed in Development/Testing for local dev and integration tests.
    /// </summary>
    private static void GuardJwtSecret(JwtSettings jwtSettings, IConfiguration configuration)
    {
        // Resolve the current environment. WebApplicationBuilder exposes it under HostDefaults.EnvironmentKey
        // ("environment") in IConfiguration, and WebApplicationFactory.UseEnvironment(...) sets that same key —
        // so the integration tests (env "Testing") are correctly detected. Fall back to the ASPNETCORE_/DOTNET_
        // env vars, then default to Production so an unconfigured deployment fails closed rather than silently
        // using the placeholder secret.
        var environment = configuration[Microsoft.Extensions.Hosting.HostDefaults.EnvironmentKey]
            ?? configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";

        var isProtectedEnvironment =
            environment.Equals("Production", StringComparison.OrdinalIgnoreCase) ||
            environment.Equals("Staging", StringComparison.OrdinalIgnoreCase);

        if (!isProtectedEnvironment)
            return;

        var secret = jwtSettings.Secret;

        if (string.IsNullOrWhiteSpace(secret) ||
            string.Equals(secret, DefaultJwtSecret, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"JwtSettings:Secret is not configured for the '{environment}' environment. " +
                "Provide a strong, unique signing key via the JwtSettings__Secret environment variable " +
                "(or a secret store). The committed default placeholder is rejected in Production/Staging.");
        }
    }

}
