namespace Learnexia.Shared.Kernel.Storage;

/// <summary>
/// MinIO object-storage configuration (platform-wide; relocated from Identity to Shared.Kernel).
/// Bound from the "MinIOConfiguration" appsettings section. The storage adapter talks to MinIO over
/// the raw S3 HTTP API with hand-rolled AWS Signature V4 (no MinIO SDK). Registered once at the Host
/// so any module can inject <see cref="Abstractions.Storage.IStorageService"/>.
/// </summary>
public class MinIOConfiguration
{
    public const string Section = "MinIOConfiguration";

    /// <summary>MinIO server endpoint (host:port), e.g. "localhost:9000".</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Access key for MinIO authentication. MUST be overridden via env in staging/prod.</summary>
    public string AccessKey { get; set; } = string.Empty;

    /// <summary>Secret key for MinIO authentication. MUST be overridden via env in staging/prod.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Whether to use SSL/TLS (https) for connections.</summary>
    public bool UseSSL { get; set; } = false;

    /// <summary>S3 signing region (default "us-east-1"). Used in the SigV4 credential scope.</summary>
    public string Region { get; set; } = "us-east-1";

    /// <summary>Default bucket used for avatar storage.</summary>
    public string DefaultBucket { get; set; } = "avatars";

    /// <summary>
    /// Default presigned-GET URL lifetime in minutes (default 60). The read path re-mints a fresh
    /// presigned URL on every request, so a short TTL is free and minimizes exposure of a child's
    /// avatar URL — callers always get a non-expired link for this window.
    /// </summary>
    public int DefaultUrlExpiryMinutes { get; set; } = 60;

    /// <summary>Maximum allowed avatar file size in bytes (default 2 MB).</summary>
    public long MaxFileSize { get; set; } = 2 * 1024 * 1024;

    /// <summary>
    /// Whether MinIO storage is enabled. When false the adapter degrades gracefully: uploads return a
    /// failure result and GetPreviewUrlAsync echoes the object key unchanged (no network calls).
    /// </summary>
    public bool Enabled { get; set; } = true;
}
