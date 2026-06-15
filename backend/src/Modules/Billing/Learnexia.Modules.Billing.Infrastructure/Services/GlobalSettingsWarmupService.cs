using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Hosting;

namespace Learnexia.Modules.Billing.Infrastructure.Services;

/// <summary>
/// <see cref="IHostedService"/> that warms up the <see cref="DbBackedGlobalSettingsProvider"/>
/// in-memory cache on application startup.
///
/// <para>Registered as a hosted service in <c>AddBillingInfrastructure</c>. On startup it calls
/// <see cref="DbBackedGlobalSettingsProvider.WarmUp"/> which bulk-loads all managed settings from
/// <c>platform.GlobalSettings</c> into the ConcurrentDictionary, so the first request does not
/// pay a per-key DB round-trip.</para>
///
/// <para>Fail-soft: a warm-up failure (e.g. DB not yet reachable) is logged and swallowed — the
/// provider falls back to lazy per-key DB loads on the first caller access.</para>
/// </summary>
public sealed class GlobalSettingsWarmupService : IHostedService
{
    private readonly DbBackedGlobalSettingsProvider _provider;
    private readonly ILoggerManager _logger;

    public GlobalSettingsWarmupService(DbBackedGlobalSettingsProvider provider, ILoggerManager logger)
    {
        _provider = provider;
        _logger   = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInfo("GlobalSettingsWarmupService: warming up settings cache.");
        _provider.WarmUp();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
