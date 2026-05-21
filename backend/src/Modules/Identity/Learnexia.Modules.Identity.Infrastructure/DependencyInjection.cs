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

        // Cross-module seams (stubs until the real adapters are provided)
        services.AddScoped<Learnexia.Shared.Contracts.Notifications.IUserNotificationService, Services.Stubs.NoOpUserNotificationService>();
        services.AddScoped<Learnexia.Shared.Contracts.Storage.IFilePreviewUrlProvider, Services.Stubs.NoOpFilePreviewUrlProvider>();

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
                // (AuthenticationIdentityService.GetClaims), so RequireRole reads them directly.
                // The seeded superadmin holds both Admin and SuperAdmin roles.
                options.AddPolicy(Learnexia.Shared.Kernel.Abstractions.AuthorizationPolicies.AdminOnly, policy =>
                    policy.RequireRole(RoleHelper.Admin, RoleHelper.SuperAdmin));
            });
            return services;
    }

}
