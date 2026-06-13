using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Ai.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAiApplication(this IServiceCollection services)
    {
        // Application layer registrations (router is wired in Infrastructure so it can access options).
        return services;
    }
}
