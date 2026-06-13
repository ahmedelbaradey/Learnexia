using Learnexia.Modules.Ai.Application;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Modules.Ai.Application.Services;
using Learnexia.Modules.Ai.Infrastructure.Gateway;
using Learnexia.Modules.Ai.Infrastructure.Providers;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Ai.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Options — binds Ai:Gateway config section.
        services.Configure<AiGatewayOptions>(
            configuration.GetSection(AiGatewayOptions.SectionName));

        // Logger (Singleton, mirrors Learning/Gamification pattern — no duplicate if already registered).
        services.AddSingleton<ILoggerManager, LoggerManager>();

        // Router (Singleton — pure, stateless mapping; safe to share).
        services.AddSingleton<IAiModelRouter, AiModelRouter>();

        // Named HttpClient — Claude.
        // BaseAddress is fixed to the Anthropic Messages API endpoint.
        // The per-call API key is injected at request time inside ClaudeProvider (never here).
        services.AddHttpClient(ClaudeProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.anthropic.com/");
            client.DefaultRequestHeaders.Add(
                "anthropic-version", "2023-06-01");
        });

        // Named HttpClient — OpenAI.
        services.AddHttpClient(OpenAiProvider.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.openai.com/");
        });

        // Provider adapters (Scoped — each request gets its own instance; HttpClient is pooled via factory).
        services.AddScoped<IAiProvider, ClaudeProvider>();
        services.AddScoped<IAiProvider, OpenAiProvider>();

        // Gateway facade (Scoped — resolves IAiProvider collection per request).
        services.AddScoped<IAiGateway, AiGateway>();

        return services;
    }
}
