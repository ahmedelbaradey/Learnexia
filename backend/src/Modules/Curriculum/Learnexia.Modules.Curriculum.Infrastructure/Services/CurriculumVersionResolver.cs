using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Modules.Curriculum.Domain.Entities;
using Learnexia.Modules.Curriculum.Domain.Enums;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// Infrastructure implementation of <see cref="ICurriculumVersionResolver"/> (BL-05-BE-12).
/// Resolves or creates the unique Draft <see cref="CurriculumVersion"/> for a (SubjectId, Language) pair.
///
/// <para><strong>Idempotency:</strong> a Draft version for (SubjectId, Language) is created only
/// if none exists. Re-ingest of the same document reuses the existing Draft.</para>
///
/// <para><strong>Draft-only:</strong> this resolver NEVER sets a version to Active. The
/// <c>CurriculumVersionStatus.Draft</c> flag is the permanent state of all BL-05 output until
/// P7-05 publishes the version. P3-07 retrieval skips Draft versions, so all ingested content
/// is invisible to students until published.</para>
/// </summary>
public sealed class CurriculumVersionResolver : ICurriculumVersionResolver
{
    private readonly CurriculumDbContext _db;
    private readonly ILoggerManager _logger;

    // System user id for audit stamping in background-service context (no HTTP user).
    private const int SystemUserId = 0;

    public CurriculumVersionResolver(CurriculumDbContext db, ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    public async Task<int> ResolveDraftVersionAsync(
        int subjectId,
        ContentLanguage language,
        CancellationToken ct = default)
    {
        // Look for an existing Draft version for this (SubjectId, Language).
        // There is no unique-index constraint limiting Draft count (only Active is filtered-unique).
        // We take the most-recently-created Draft (lowest penalty on re-ingest).
        var existing = await _db.CurriculumVersions
            .AsNoTracking()
            .Where(v => v.SubjectId == subjectId &&
                        v.Language  == language  &&
                        v.Status    == CurriculumVersionStatus.Draft)
            .OrderByDescending(v => v.Id)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            _logger.LogInfo(
                $"CurriculumVersionResolver: reusing Draft CurriculumVersion id={existing.Id} " +
                $"subjectId={subjectId} language={language}.");
            return existing.Id;
        }

        // Create a new Draft version.
        // Name convention: "Ingest-Draft-{subjectId}-{language}-{utcDate}" — stable, human-readable.
        var now  = DateTimeOffset.UtcNow;
        var name = $"Ingest-Draft-{subjectId}-{language}-{now:yyyyMMdd}";

        var version = new CurriculumVersion
        {
            SubjectId = subjectId,
            Language  = language,
            Status    = CurriculumVersionStatus.Draft,
            Name      = name,
        };
        _db.CurriculumVersions.Add(version);
        await _db.SaveChangesAsync(SystemUserId);

        _logger.LogInfo(
            $"CurriculumVersionResolver: created Draft CurriculumVersion id={version.Id} " +
            $"name='{name}' subjectId={subjectId} language={language}.");

        return version.Id;
    }
}
