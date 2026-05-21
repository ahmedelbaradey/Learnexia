using Microsoft.Extensions.Options;

namespace Learnexia.Host.SystemConfiguration;

internal static class SystemConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapSystemConfiguration(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/system/configuration",
            (IOptionsSnapshot<SystemConfigurationOptions> options) => Results.Ok(options.Value))
            .WithTags("System");

        return app;
    }
}
