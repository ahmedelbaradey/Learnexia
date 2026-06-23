namespace Learnexia.Shared.Kernel.Abstractions.Storage;

/// <summary>
/// Platform-wide file-storage seam (relocated from the Identity module — it is a shared capability for
/// ANY file upload, not avatar-specific). Backed by a MinIO adapter that speaks the raw S3 HTTP API with
/// hand-rolled AWS Signature V4 (no MinIO SDK). Registered ONCE at the Host so every module can inject it.
///
/// The upload signature is STREAM-based (not IFormFile) on purpose: it keeps Shared.Kernel free of an
/// ASP.NET framework reference. ASP.NET callers pass file.OpenReadStream() / file.ContentType / file.Length.
///
/// Note: object DELETE is intentionally NOT part of this contract for the MVP. Orphaned objects after a
/// remove are an accepted MVP trade-off; add a delete method + background sweep as a follow-up.
/// </summary>
public interface IStorageService
{
    /// <summary>Uploads a stream (PUT) under the given object key in the given bucket.</summary>
    Task<StorageResult> UploadFileAsync(Stream content, string fileName, string contentType, long length, string bucketName, CancellationToken ct = default);

    /// <summary>Downloads an object (GET) into memory.</summary>
    Task<FileDownloadResult> DownloadFileAsync(string objectKey, string bucketName, CancellationToken ct = default);

    /// <summary>
    /// Builds a presigned GET URL (query-string SigV4) for the object — a pure computation, no network
    /// call. When storage is disabled, returns the object key unchanged so callers degrade gracefully.
    /// </summary>
    Task<string> GetPreviewUrlAsync(string objectKey, string bucketName, int expiryInMinutes = 60, CancellationToken ct = default);

    /// <summary>Checks whether an object exists (HEAD → 200 true / 404 false).</summary>
    Task<bool> FileExistsAsync(string objectKey, string bucketName, CancellationToken ct = default);

    /// <summary>
    /// Ensures a bucket exists (HEAD → exists; 404 → PUT to create).
    /// Fail-soft: never throws — logs the outcome and returns a boolean indicating success.
    /// Called at Host startup to provision module-specific buckets (e.g. "curriculum").
    /// </summary>
    Task<bool> EnsureBucketAsync(string bucketName, CancellationToken ct = default);
}

/// <summary>Result of a file-upload operation. On success carries the stored object key (FilePath).</summary>
public class StorageResult
{
    public bool Success { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
}

/// <summary>Result of a file-download operation. On success carries the bytes + content metadata.</summary>
public class FileDownloadResult
{
    public bool Success { get; set; }
    public byte[]? FileBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}
