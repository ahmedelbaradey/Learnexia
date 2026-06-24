using Learnexia.Modules.Curriculum.Domain.Enums;

namespace Learnexia.Modules.Curriculum.Application.Abstractions;

/// <summary>
/// Resolves (or creates) the unique Draft <see cref="Domain.Entities.CurriculumVersion"/>
/// for a given (SubjectId, Language) pair (BL-05-BE-12).
///
/// <para><strong>Draft-only rule (Q7 decision):</strong> BL-05 ingestion ONLY writes into a
/// Draft version. A Draft version is created if none exists for the (SubjectId, Language) pair.
/// If a Draft already exists, it is reused (idempotent re-ingest). This method NEVER creates
/// an Active version — P7-05 (the publish surface) owns the Draft→Active transition.</para>
///
/// <para>P3-07 retrieval filters on <c>CurriculumVersion.Status = Active</c>, so all BL-05
/// ingested content is invisible to students until P7-05 publishes (AC13).</para>
///
/// <para>Registered in Curriculum DI as Scoped (depends on CurriculumDbContext).</para>
/// </summary>
public interface ICurriculumVersionResolver
{
    /// <summary>
    /// Returns the Id of the existing Draft <c>CurriculumVersion</c> for
    /// <paramref name="subjectId"/> + <paramref name="language"/>, or creates a new one
    /// and returns its Id.
    /// </summary>
    /// <param name="subjectId">Learning module Subject.Id (plain int — no cross-module FK).</param>
    /// <param name="language">Content language of the version.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Draft CurriculumVersion.Id (created or existing).</returns>
    Task<int> ResolveDraftVersionAsync(
        int subjectId,
        ContentLanguage language,
        CancellationToken ct = default);
}
