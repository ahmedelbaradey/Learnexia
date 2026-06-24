using Learnexia.Shared.Contracts.Learning;

namespace Learnexia.Modules.Curriculum.Application.Abstractions;

/// <summary>
/// Mockable test seam for graph inference (BL-03-BE-2).
///
/// <para><strong>IMPORTANT — this is a test seam ONLY.</strong>
/// Per Q2 decision (BL-03 plan), the real inference transport is the DB-outbox
/// (<c>JobType='infer_edges'</c>): Python polls the job, writes <c>ResultJson</c>,
/// and <see cref="Jobs.EdgeInferenceAdvanceService"/> claims the result asynchronously.
/// This interface is NOT used for live calls — it is here so the integration tests and
/// <see cref="Commands.BuildKGSuggestions.BuildKnowledgeGraphSuggestionsCommandHandler"/>
/// can inject a deterministic stub without starting the Python worker.</para>
///
/// <para>Live inference: Python claims <c>PipelineJob{JobType='infer_edges', Status='Pending'}</c>,
/// runs LightRAG/mock, writes inferred edges to <c>ResultJson</c>, transitions to Done/Failed.
/// The .NET poller (<see cref="Jobs.EdgeInferenceAdvanceService"/>) advances the result.</para>
/// </summary>
public interface IGraphInferenceClient
{
    /// <summary>
    /// Mock-only: returns a deterministic empty result (no live HTTP call).
    /// The real inference path is the DB-outbox (Python outbox poller + EdgeInferenceAdvanceService).
    /// </summary>
    /// <param name="nodes">The learning nodes to infer edges for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     In the mock implementation always returns an empty <see cref="GraphInferenceResult"/>.
    ///     In a future sync path, this would call the inference service directly.
    /// </returns>
    Task<GraphInferenceResult> InferEdgesAsync(
        IReadOnlyList<KnowledgeNodeSummaryDto> nodes,
        CancellationToken ct = default);
}

/// <summary>
/// Result returned by <see cref="IGraphInferenceClient.InferEdgesAsync"/>.
/// In the outbox-lane flow this is always empty; it is populated only in the sync path (future).
/// </summary>
/// <param name="InferenceModel">Identifier of the model that produced this result (e.g. "mock" or "lightrag-v1").</param>
/// <param name="InferredEdges">The candidate edges. Empty in the mock/outbox path.</param>
public sealed record GraphInferenceResult(
    string InferenceModel,
    IReadOnlyList<InferredEdgeDto> InferredEdges);

/// <summary>
/// A single candidate edge returned by inference (wire format mirrors the Python ResultJson).
/// </summary>
/// <param name="SourceSkillKey">Source node SkillKey (Python-emitted stable identifier).</param>
/// <param name="TargetSkillKey">Target node SkillKey.</param>
/// <param name="RelationshipType">
///     Wire string: "Prerequisite" or "Related". Maps to <c>CurriculumRelationshipType</c>.
/// </param>
/// <param name="Strength">Edge strength in [0, 1] (server re-clamps before use).</param>
/// <param name="Confidence">Model confidence in [0, 1] (server re-clamps before use).</param>
public sealed record InferredEdgeDto(
    string SourceSkillKey,
    string TargetSkillKey,
    string RelationshipType,
    decimal Strength,
    decimal Confidence);
