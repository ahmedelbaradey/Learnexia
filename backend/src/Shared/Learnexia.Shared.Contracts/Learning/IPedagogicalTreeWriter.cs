namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module synchronous write seam: Learning implements, Curriculum (ingest-advance)
/// calls to upsert hierarchy nodes.
///
/// <para><strong>Decision Q1 (BL-05):</strong> The curriculum module must NOT write to the
/// learning schema directly (CLAUDE.md rule 1 — module isolation). Instead the curriculum
/// ingest-advance service calls this interface to create/find hierarchy nodes by natural key
/// and receive the database IDs back, which it then stores as plain int references on
/// <c>CurriculumChunk</c> and <c>ProvenanceMapping</c> rows (no cross-module FK).</para>
///
/// <para><strong>Idempotency contract:</strong> every Ensure* method performs a natural-key
/// upsert. Re-ingesting a document with the same content must return the existing IDs rather
/// than creating duplicates. The natural keys are:
/// <list type="bullet">
///   <item>Subject: <c>(GradeId, SubjectCode, Language)</c></item>
///   <item>Unit: <c>(Name, SubjectId)</c></item>
///   <item>Lesson: <c>(Name, UnitId)</c></item>
///   <item>Concept: <c>(Name, SubjectId)</c></item>
///   <item>Skill: <c>(Name, ConceptId)</c></item>
/// </list>
/// These mirror the keys used in <c>LearningSeeder.EnsureXAsync</c> — running ingest on
/// hand-seeded P2-10 demo data returns the existing IDs and leaves them untouched.</para>
///
/// <para><strong>SkillKey ownership:</strong> <c>EnsureSkillAsync</c> accepts the stable
/// <c>skillKey</c> produced by the Python worker. If the skill already exists (matched by
/// name+conceptId) and its SkillKey is null, the implementation sets it on the existing row.
/// If a skill is newly created, the SkillKey is set at creation time. This preserves the
/// immutability invariant: once non-null, SkillKey is not overwritten.</para>
///
/// <para><strong>Isolation preserved:</strong> consumers reference this interface via
/// <c>Shared.Contracts</c> only — no project reference from Curriculum to Learning.
/// All returned IDs are plain <c>int</c> — no cross-module FK/navigation property.</para>
///
/// <para><strong>Not an INotification:</strong> this is a synchronous awaited call, not a
/// fire-and-forget MediatR notification. The curriculum ingest-advance service needs the
/// returned IDs synchronously to link chunks and provenance mappings in the same transaction
/// (BL-05 plan §Seam sequencing).</para>
///
/// <para>Registered in <c>AddLearningInfrastructure()</c> as Scoped.</para>
/// </summary>
public interface IPedagogicalTreeWriter
{
    /// <summary>
    /// Ensures a Subject exists for the given (GradeId, SubjectCode, Language) triplet.
    /// Returns the existing Subject.Id if found, or creates a new one and returns its Id.
    /// </summary>
    /// <param name="gradeId">Learning module Grade.Id (no cross-module FK).</param>
    /// <param name="subjectCode">SubjectCode enum value (int) — cast from Learning.Domain.Enums.SubjectCode.</param>
    /// <param name="language">ContentLanguage enum value (int).</param>
    /// <param name="displayName">Display name for the subject (used if creating a new one).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Subject.Id (created or existing).</returns>
    Task<int> EnsureSubjectAsync(
        int gradeId,
        int subjectCode,
        int language,
        string displayName,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures a Unit exists for the given (Name, SubjectId) pair.
    /// Returns the existing Unit.Id if found, or creates a new one and returns its Id.
    /// </summary>
    /// <param name="name">Unit name (idempotency key together with subjectId).</param>
    /// <param name="subjectId">Learning module Subject.Id returned by <see cref="EnsureSubjectAsync"/>.</param>
    /// <param name="sequenceOrder">Display order within the subject (used if creating a new one).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Unit.Id (created or existing).</returns>
    Task<int> EnsureUnitAsync(
        string name,
        int subjectId,
        int sequenceOrder,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures a Lesson exists for the given (Name, UnitId) pair.
    /// Returns the existing Lesson.Id if found, or creates a new one and returns its Id.
    /// </summary>
    /// <param name="name">Lesson name (idempotency key together with unitId).</param>
    /// <param name="unitId">Learning module Unit.Id returned by <see cref="EnsureUnitAsync"/>.</param>
    /// <param name="sequenceOrder">Display order within the unit (used if creating a new one).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Lesson.Id (created or existing).</returns>
    Task<int> EnsureLessonAsync(
        string name,
        int unitId,
        int sequenceOrder,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures a Concept exists for the given (Name, SubjectId) pair.
    /// Returns the existing Concept.Id if found, or creates a new one and returns its Id.
    /// </summary>
    /// <param name="name">Concept name (idempotency key together with subjectId).</param>
    /// <param name="subjectId">Learning module Subject.Id returned by <see cref="EnsureSubjectAsync"/>.</param>
    /// <param name="description">Optional description (used if creating a new one).</param>
    /// <param name="difficulty">Difficulty level int (0–4, cast from Learning.Domain.Enums.DifficultyLevel).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The Concept.Id (created or existing).</returns>
    Task<int> EnsureConceptAsync(
        string name,
        int subjectId,
        string? description,
        int difficulty,
        CancellationToken ct = default);

    /// <summary>
    /// Ensures a Skill exists for the given (Name, ConceptId) pair.
    /// Returns the existing Skill.Id if found, or creates a new one and returns its Id.
    /// Also ensures the KnowledgeNode for this skill exists (SkillKey-anchored idempotency).
    /// </summary>
    /// <param name="name">Skill name (idempotency key together with conceptId).</param>
    /// <param name="conceptId">Learning module Concept.Id returned by <see cref="EnsureConceptAsync"/>.</param>
    /// <param name="skillKey">
    /// Stable semantic key (e.g. "math.grade4.fractions.add-unlike-denominators").
    /// Set at creation; if a matching skill already exists with a null SkillKey, this value is applied.
    /// If the skill already has a non-null SkillKey, it is NOT overwritten (immutability contract).
    /// </param>
    /// <param name="masteryThreshold">Mastery percentage threshold (0–100) for this skill.</param>
    /// <param name="estimatedTimeMinutes">Estimated learning time in minutes.</param>
    /// <param name="subjectId">Subject.Id for KnowledgeNode association.</param>
    /// <param name="gradeId">Grade.Id for KnowledgeNode association.</param>
    /// <param name="difficulty">Difficulty level (1–5 int scale for KnowledgeNode).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="EnsureSkillResult"/> carrying the Skill.Id and the corresponding KnowledgeNode.Id.
    /// Both are returned because the curriculum ingest-advance stores both as plain int references.
    /// </returns>
    Task<EnsureSkillResult> EnsureSkillAsync(
        string name,
        int conceptId,
        string skillKey,
        int masteryThreshold,
        int estimatedTimeMinutes,
        int subjectId,
        int gradeId,
        int difficulty,
        CancellationToken ct = default);
}

/// <summary>
/// Result of <see cref="IPedagogicalTreeWriter.EnsureSkillAsync"/> — carries both the Skill.Id
/// and the KnowledgeNode.Id so the curriculum caller can store both as plain int references
/// on <c>CurriculumChunk.SkillId</c> and <c>ProvenanceMapping.PedagogicalNodeId</c>.
/// </summary>
/// <param name="SkillId">The Learning module Skill.Id (created or existing).</param>
/// <param name="KnowledgeNodeId">
/// The Learning module KnowledgeNode.Id wrapping this skill (created or existing).
/// May be 0 if KnowledgeNode creation fails non-fatally (caller logs and continues
/// — the chunk is still written; the KG edge is optional at ingest time).
/// </param>
public record EnsureSkillResult(int SkillId, int KnowledgeNodeId);
