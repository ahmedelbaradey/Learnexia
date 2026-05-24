using Learnexia.Shared.Kernel.Abstractions.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Shared.Kernel.Storage;

/// <summary>
/// MinIO storage registration (platform-wide; relocated from Identity to Shared.Kernel). Binds
/// MinIOConfiguration and registers <see cref="IStorageService"/> → <see cref="StorageService"/> backed
/// by a typed HttpClient. No MinIO SDK is referenced — the adapter talks the raw S3 HTTP API with
/// hand-rolled AWS SigV4. Called ONCE at the Host so every module can inject IStorageService.
/// </summary>
public static class MinIODependencies
{
    public static IServiceCollection AddMinIODependencies(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<MinIOConfiguration>(configuration.GetSection(MinIOConfiguration.Section));

        // Typed HttpClient — DI injects it into StorageService. The adapter signs every request itself,
        // so no DelegatingHandler is needed; the client is just the transport.
        services.AddHttpClient<IStorageService, StorageService>();

        return services;
    }
}
