namespace Learnexia.Modules.Curriculum.Application.Abstractions;

/// <summary>
/// Mockable test seam for the Python curriculum-intelligence ingestion worker (BL-05-BE-3).
///
/// <para><strong>No live HTTP call is made via this interface.</strong>
/// The cross-process transport is the <c>curriculum.PipelineJobs</c> DB-outbox (ADR-0004 §4b,
/// Q6 decision). The Python ingest worker polls the outbox directly — there is no
/// .NET→Python HTTP endpoint on the ingestion path.
/// <see cref="IIngestionServiceClient"/> exists ONLY as a test seam so that the api-tester
/// can simulate completed ingest jobs without a running Python worker — exactly mirroring
/// <see cref="IParsingServiceClient"/> for the parse path (BL-02-BE-2).</para>
///
/// <para>The only registered implementation is <c>NoOpIngestionServiceClient</c> (mock/no-op).
/// A live HTTP implementation is NOT planned — the Python side is self-driven by polling.</para>
/// </summary>
public interface IIngestionServiceClient
{
    /// <summary>
    /// Simulate/retrieve the ingestion result for the given job (test seam only).
    /// Returns a deterministic <see cref="IngestionJobResult"/> in the mock implementation.
    /// </summary>
    /// <param name="jobId">The <c>PipelineJob.Id</c> whose result is being retrieved.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<IngestionJobResult?> GetIngestionResultAsync(int jobId, CancellationToken ct = default);
}

/// <summary>
/// Represents the structured ingestion result that the Python worker writes into
/// <c>PipelineJob.ResultJson</c> and the .NET ingest-advance poller reads (BL-05-BE-13).
///
/// <para>Shape mirrors the DB-outbox contract from <c>docs/briefs/BL-05.md §Handoff python-worker</c>:
/// <code>
/// {
///   "nodes": [{ "type": "skill", "name": "...", "skill_key": "...", ... }],
///   "chunks": [{ "content": "...", "metadata": {}, ... }],
///   "flags": [{ "suggested_classification": "...", "confidence": 0.65, ... }]
/// }
/// </code>
/// </para>
/// </summary>
/// <param name="Nodes">Hierarchy nodes extracted by the Python worker.</param>
/// <param name="Chunks">Content chunks extracted by the Python worker.</param>
/// <param name="Flags">Low-confidence flags for review routing.</param>
public record IngestionJobResult(
    IReadOnlyList<IngestedNodeItem> Nodes,
    IReadOnlyList<IngestedChunkItem> Chunks,
    IReadOnlyList<IngestionFlagItem> Flags);

/// <summary>
/// One hierarchy node in the ingestion result <c>nodes[]</c> array.
/// Produced by the Python Claude hierarchy extractor (PY-2).
/// </summary>
public record IngestedNodeItem(
    /// <summary>Node type: "subject" | "unit" | "lesson" | "concept" | "skill".</summary>
    string Type,
    /// <summary>Display name of the node.</summary>
    string Name,
    /// <summary>
    /// Stable semantic key (SkillKey format: {subject_code}.grade{N}.{unit_slug}.{skill_slug}).
    /// Present on skill nodes; null or empty for parent hierarchy nodes.
    /// </summary>
    string? SkillKey,
    /// <summary>Confidence score in [0, 1] assigned by the Python worker.</summary>
    decimal Confidence,
    /// <summary>Grade level (number, e.g. 4 for Grade 4).</summary>
    int GradeLevel,
    /// <summary>Subject name matching one of: Math, Science, Arabic, English.</summary>
    string SubjectName,
    /// <summary>Subject code int matching Learning.Domain.Enums.SubjectCode.</summary>
    int SubjectCode,
    /// <summary>Language int matching Curriculum.Domain.Enums.ContentLanguage.</summary>
    int Language,
    /// <summary>Difficulty on 1–5 scale.</summary>
    int Difficulty,
    /// <summary>Parent node name (used to link units to subjects, concepts to subjects, skills to concepts).</summary>
    string? ParentName,
    /// <summary>Sequence order hint (1-based).</summary>
    int SequenceOrder,
    /// <summary>Optional description for concept/unit nodes.</summary>
    string? Description);

/// <summary>
/// One content chunk in the ingestion result <c>chunks[]</c> array.
/// Produced by the Python Arabic-boundary chunker (PY-3).
/// </summary>
public record IngestedChunkItem(
    /// <summary>Text content of the chunk (tashkeel preserved).</summary>
    string Content,
    /// <summary>JSON metadata string (e.g. grade/subject context).</summary>
    string? Metadata,
    /// <summary>Difficulty on 1–5 scale.</summary>
    int Difficulty,
    /// <summary>Source reference (page/block within the source document).</summary>
    string? SourceReference,
    /// <summary>Grade id hint (number; .NET must map to Grade.Id via the learning tree).</summary>
    int GradeLevel,
    /// <summary>Subject code int matching Learning.Domain.Enums.SubjectCode.</summary>
    int SubjectCode,
    /// <summary>Language int matching Curriculum.Domain.Enums.ContentLanguage.</summary>
    int Language,
    /// <summary>SkillKey of the skill this chunk is linked to (if resolved).</summary>
    string? SkillKey,
    /// <summary>Confidence score in [0, 1].</summary>
    decimal Confidence);

/// <summary>
/// One low-confidence flag in the ingestion result <c>flags[]</c> array.
/// Produced by the Python confidence scorer (PY-4) for items below the review threshold.
/// </summary>
public record IngestionFlagItem(
    /// <summary>Human-readable proposed hierarchy classification.</summary>
    string SuggestedClassification,
    /// <summary>Confidence score in [0, 1].</summary>
    decimal Confidence,
    /// <summary>Source reference locating the material in the original document.</summary>
    string? SourceReference,
    /// <summary>Full structured payload (proposed node content) as JSON string.</summary>
    string? PayloadJson);
