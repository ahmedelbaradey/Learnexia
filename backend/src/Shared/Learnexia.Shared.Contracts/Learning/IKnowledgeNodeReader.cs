namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module synchronous read seam: Learning implements, Curriculum calls to read
/// <c>learning.KnowledgeNode</c> rows without any direct project reference.
///
/// <para><strong>BL-03 usage:</strong>
/// <list type="bullet">
///   <item><see cref="GetNodesForSubjectAsync"/>: called by <c>BuildKnowledgeGraphSuggestionsCommand</c>
///         to load nodes for a subject/grade, shape them as <see cref="KnowledgeNodeSummaryDto"/>
///         (with SkillKey), and write them to a <c>PipelineJob{JobType='infer_edges'}</c> payload
///         for the Python worker.</item>
///   <item><see cref="GetNodesBySkillKeysAsync"/>: called by <c>EdgeInferenceAdvanceService</c>
///         to resolve the Python-emitted SkillKeys to integer KnowledgeNode.Ids before writing
///         <c>KGSuggestion</c> rows. Unresolved keys are dropped + logged (fail-soft).</item>
/// </list>
/// </para>
///
/// <para><strong>Module isolation:</strong> consumers reference this interface via
/// <c>Shared.Contracts</c> only — no project reference from Curriculum to Learning.
/// All returned IDs are plain <c>int</c> — no cross-module FK/navigation property.</para>
///
/// <para>Registered in <c>AddLearningInfrastructure()</c> as Scoped (mirrors PedagogicalTreeWriterAdapter).</para>
/// </summary>
public interface IKnowledgeNodeReader
{
    /// <summary>
    /// Returns all live (non-deleted) KnowledgeNodes for the given SubjectCode + GradeId combination.
    /// Used by <c>BuildKnowledgeGraphSuggestionsCommand</c> to feed inference.
    /// </summary>
    /// <param name="subjectCode">SubjectCode int value (0=Math, 1=Science, 2=Arabic, 3=English). Matches Learning's SubjectCode enum.</param>
    /// <param name="gradeId">Learning module Grade.Id (no cross-module FK).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Materialized list; may be empty if no nodes exist for the subject/grade.</returns>
    Task<IReadOnlyList<KnowledgeNodeSummaryDto>> GetNodesForSubjectAsync(
        int subjectCode,
        int gradeId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns nodes whose <c>SkillKey</c> is in the provided set.
    /// Used by <c>EdgeInferenceAdvanceService</c> to resolve Python SkillKeys → node ids.
    /// Unresolved SkillKeys (no matching node) are simply absent from the result — the caller
    /// logs and drops them (fail-soft, mirror of IngestJobAdvanceService's below-threshold handling).
    /// </summary>
    /// <param name="skillKeys">The SkillKey values to resolve (e.g. "math.grade4.fractions.add-unlike-denominators").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Materialized list of matching nodes. May be smaller than <paramref name="skillKeys"/> if some are unresolved.</returns>
    Task<IReadOnlyList<KnowledgeNodeSummaryDto>> GetNodesBySkillKeysAsync(
        IEnumerable<string> skillKeys,
        CancellationToken ct = default);
}

/// <summary>
/// Read-only summary of a <c>learning.KnowledgeNode</c> row, emitted by
/// <see cref="IKnowledgeNodeReader"/> for cross-module consumers (e.g., Curriculum).
///
/// All fields are plain value types — no navigation properties, no cross-module FKs.
/// </summary>
/// <param name="NodeId">KnowledgeNode.Id in the learning schema.</param>
/// <param name="SkillKey">Stable semantic key (may be null for older nodes; callers must handle null).</param>
/// <param name="Name">Display name of the node.</param>
/// <param name="NodeType">KnowledgeNodeType int (0=Skill, 1=Concept, 2=ReviewCheckpoint — mirrors learning enum).</param>
/// <param name="SubjectCode">SubjectCode int (0=Math, 1=Science, 2=Arabic, 3=English).</param>
/// <param name="GradeId">Grade.Id (learning module). Plain int, no FK.</param>
/// <param name="GradeNumber">Grade number (1–6) — the actual school grade number, NOT the Grade.Id FK.
/// Used by the BL-03 infer_edges PayloadJson emitter to write <c>grade</c> per node as Python expects.</param>
/// <param name="Difficulty">Difficulty on a 1–5 scale (plain int).</param>
public sealed record KnowledgeNodeSummaryDto(
    int NodeId,
    string? SkillKey,
    string Name,
    int NodeType,
    int SubjectCode,
    int GradeId,
    int GradeNumber,
    int Difficulty);
