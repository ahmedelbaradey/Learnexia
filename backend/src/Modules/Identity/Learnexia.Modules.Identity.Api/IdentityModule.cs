using Learnexia.Modules.Identity.Api.Controllers;
using Learnexia.Modules.Identity.Application;
using Learnexia.Modules.Identity.Infrastructure;
using Learnexia.Modules.Identity.Infrastructure.Persistence.Seed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Identity.Api;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddIdentityApplication();
        services.AddIdentityInfrastructure(configuration);

        // Register this module's controllers as an application part so the Host discovers them.
        services.AddControllers()
        .AddApplicationPart(typeof(AuthenticationController).Assembly)
        .AddApplicationPart(typeof(AuthorzationController).Assembly)
        .AddApplicationPart(typeof(UserManagementController).Assembly);

        return services;
    }

    // Host-callable seed hook (roles + users). Idempotent; safe to run on every startup.
    public static Task SeedAsync(IServiceProvider serviceProvider) => IdentitySeeder.SeedAsync(serviceProvider);
}
