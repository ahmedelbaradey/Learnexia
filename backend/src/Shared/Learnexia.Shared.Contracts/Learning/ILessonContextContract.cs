namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module seam: allows the AI module to read minimal lesson metadata (title, subject,
/// grade) from the Learning module without a cross-module FK or project reference.
///
/// <para>Implemented by <c>LessonContextContractAdapter</c> in
/// <c>Learnexia.Modules.Learning.Infrastructure</c>, registered in
/// <c>AddLearningInfrastructure</c>. The Ai handler injects this interface — it never
/// references Learning's projects directly (module isolation rule).</para>
///
/// <para>Returns <c>null</c> when the lesson does not exist or is soft-deleted, so callers
/// must handle the null case (no exception is thrown).</para>
/// </summary>
public interface ILessonContextContract
{
    /// <summary>
    /// Returns minimal lesson context for <paramref name="lessonId"/>, or <c>null</c> when
    /// the lesson is not found.
    /// </summary>
    Task<LessonContextDto?> GetLessonContextAsync(int lessonId, CancellationToken ct = default);
}
