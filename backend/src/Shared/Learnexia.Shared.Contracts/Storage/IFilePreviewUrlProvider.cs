namespace Learnexia.Shared.Contracts.Storage;

// Cross-module seam: converts a stored file path into a preview URL (e.g. MinIO presigned URL),
// without Identity referencing the storage/infrastructure module.
public interface IFilePreviewUrlProvider
{
    Task<string> GeneratePreviewUrlAsync(string? filePath, int moduleId, CancellationToken cancellationToken = default);
}
