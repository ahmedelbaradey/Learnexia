using Learnexia.Modules.Curriculum.Infrastructure.Configuration;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Abstractions.Storage;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// Ensures the curriculum MinIO bucket exists at application startup (BL-01 BE-4/Q1).
///
/// <para>There is NO existing bucket-ensure routine in the repo (<c>avatars</c> is assumed
/// pre-provisioned). This service delegates to <see cref="IStorageService.EnsureBucketAsync"/>
/// (a new platform-wide method added to the storage seam) and logs the outcome.</para>
///
/// <para>Called from <see cref="Learnexia.Modules.Curriculum.Api.CurriculumModule.InitializeAsync"/>
/// ONCE per Host startup. Fail-soft: a failure here is logged but does NOT crash startup —
/// uploads will report a storage error at runtime if the bucket is absent.</para>
/// </summary>
public sealed class CurriculumBucketEnsureService
{
    private readonly IStorageService _storageService;
    private readonly CurriculumUploadConfiguration _uploadConfig;
    private readonly ILoggerManager _logger;

    public CurriculumBucketEnsureService(
        IStorageService storageService,
        IOptions<CurriculumUploadConfiguration> uploadConfig,
        ILoggerManager logger)
    {
        _storageService = storageService;
        _uploadConfig   = uploadConfig.Value;
        _logger         = logger;
    }

    /// <summary>
    /// Idempotent: no-op if the bucket already exists. Logs outcome, never throws.
    /// </summary>
    public async Task EnsureBucketAsync(CancellationToken cancellationToken = default)
    {
        var bucketName = _uploadConfig.BucketName;
        _logger.LogInfo($"BL-01 BucketEnsure: ensuring curriculum bucket '{bucketName}' exists...");

        var created = await _storageService.EnsureBucketAsync(bucketName, cancellationToken);

        if (!created)
        {
            _logger.LogWarn(
                $"BL-01 BucketEnsure: could not ensure bucket '{bucketName}'. " +
                "Curriculum uploads will fail at runtime if the bucket does not exist.");
        }
    }
}
