using Learnexia.Shared.Contracts.Storage;

namespace Learnexia.Modules.Identity.Infrastructure.Services.Stubs;

// NOTE: stub. backend/ generates MinIO presigned URLs. Replace with a real storage adapter.
// Until then the original file path is returned unchanged.
public sealed class NoOpFilePreviewUrlProvider : IFilePreviewUrlProvider
{
    public Task<string> GeneratePreviewUrlAsync(string? filePath, int moduleId, CancellationToken cancellationToken = default)
        => Task.FromResult(filePath ?? string.Empty);
}
