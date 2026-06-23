namespace Learnexia.Modules.Curriculum.Infrastructure.Configuration;

/// <summary>
/// Configuration for curriculum file uploads (BL-01 BE-3).
/// Bound from the <c>"CurriculumUpload"</c> appsettings section.
///
/// <para><see cref="MaxFileSizeBytes"/> defaults to 100 MB (104_857_600 bytes) and is
/// config-driven so it can be tuned without a redeploy.</para>
/// <para><see cref="BucketName"/> is the MinIO bucket for curriculum files — separate from
/// the <c>avatars</c> bucket (different lifecycle/ACL). Default <c>"curriculum"</c>.</para>
/// </summary>
public class CurriculumUploadConfiguration
{
    public const string Section = "CurriculumUpload";

    /// <summary>
    /// Maximum allowed curriculum file size in bytes.
    /// Default: 100 MB = 104_857_600 bytes.
    /// Override via <c>CurriculumUpload:MaxFileSizeBytes</c> in appsettings.
    /// </summary>
    public long MaxFileSizeBytes { get; set; } = 100L * 1024 * 1024;

    /// <summary>
    /// MinIO bucket name for curriculum files. Default: <c>"curriculum"</c>.
    /// This bucket is separate from <c>avatars</c> — different lifecycle and ACL.
    /// Override via <c>CurriculumUpload:BucketName</c> in appsettings.
    /// </summary>
    public string BucketName { get; set; } = "curriculum";
}
