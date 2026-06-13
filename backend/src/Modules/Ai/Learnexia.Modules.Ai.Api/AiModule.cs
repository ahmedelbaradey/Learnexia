using Learnexia.Modules.Ai.Application;
using Learnexia.Modules.Ai.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Ai.Api;

/// <summary>
/// Module entry point for the Ai module. Called from the Host <c>Program.cs</c> via
/// <c>builder.Services.AddAiModule(builder.Configuration)</c>.
///
/// P3-01 ships NO HTTP endpoint (P3-04 owns the first AI endpoint). This module registers
/// the gateway seam (<see cref="Learnexia.Shared.Contracts.Ai.IAiGateway"/>) for all
/// other modules to inject.
/// </summary>
public static class AiModule
{
    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAiApplication();
        services.AddAiInfrastructure(configuration);
        return services;
    }
}
