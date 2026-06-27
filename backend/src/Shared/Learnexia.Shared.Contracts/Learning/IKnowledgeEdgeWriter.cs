namespace Learnexia.Shared.Contracts.Learning;

/// <summary>
/// Cross-module synchronous write seam: Learning implements, Curriculum (KGSuggestion approve)
/// calls to publish a single approved edge into <c>learning.KnowledgeEdge</c>.
///
/// <para><strong>Decision E chokepoint (BL-03):</strong> This is the ONLY code path that writes
/// a new <c>KnowledgeEdge</c> from outside the <c>learning</c> module. Every guard that
/// <c>AddKnowledgeEdgeCommandHandler</c> enforces is re-enforced inside the implementation:
/// <list type="number">
///   <item>Both node IDs must resolve to existing KnowledgeNodes.</item>
///   <item>Cross-language guard: both nodes must belong to the same language.</item>
///   <item>Duplicate guard: the triple (SourceNodeId, TargetNodeId, RelationshipType) must be unique.</item>
///   <item>Acyclic guard: for Prerequisite edges, <c>SkillGraphValidator.AssertAcyclic</c> runs before insert.</item>
/// </list>
/// </para>
///
/// <para><strong>Return contract:</strong>
/// <see cref="EdgePublishResult"/> carries typed flags so the curriculum approve handler can map
/// to the correct HTTP status: 200 (Published or Duplicate), 409 (duplicate edge), 422 (cycle or
/// cross-language). The caller is responsible for mapping; this interface returns result, not throws.
/// </para>
///
/// <para><strong>Not an INotification:</strong> synchronous awaited call. The approve handler
/// needs the publish outcome synchronously to decide whether to stamp <c>Approved</c>
/// (BL-05 precedent: IPedagogicalTreeWriter is also not fire-and-forget).</para>
///
/// <para>Registered in <c>AddLearningInfrastructure()</c> as Scoped (mirrors PedagogicalTreeWriterAdapter).</para>
/// </summary>
public interface IKnowledgeEdgeWriter
{
    /// <summary>
    /// Attempts to publish an approved KGSuggestion as a <c>KnowledgeEdge</c> into the learning module.
    /// Encapsulates the full guard chain (cross-language, duplicate, acyclic).
    /// </summary>
    /// <param name="sourceNodeId">Source (prerequisite / origin) KnowledgeNode.Id.</param>
    /// <param name="targetNodeId">Target (dependent / destination) KnowledgeNode.Id.</param>
    /// <param name="relationshipType">
    ///     Relationship type int value. Maps to <c>learning.EdgeRelationshipType</c>:
    ///     0 = Prerequisite, 1 = Related. Must align with <c>CurriculumRelationshipType</c> values.
    /// </param>
    /// <param name="strength">Edge strength in [0, 1] (clamped by the caller before passing here).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    ///     <see cref="EdgePublishResult"/> indicating the outcome. The caller maps to HTTP status:
    ///     <list type="bullet">
    ///       <item><see cref="EdgePublishResult.Published"/> = true → 200 OK.</item>
    ///       <item><see cref="EdgePublishResult.Duplicate"/> = true → treat as success (idempotent), 200 OK.</item>
    ///       <item><see cref="EdgePublishResult.Cycle"/> = true → 422 domain error, nothing committed.</item>
    ///       <item><see cref="EdgePublishResult.CrossLanguage"/> = true → 422 domain error, nothing committed.</item>
    ///     </list>
    /// </returns>
    Task<EdgePublishResult> PublishApprovedEdgeAsync(
        int sourceNodeId,
        int targetNodeId,
        int relationshipType,
        decimal strength,
        CancellationToken ct = default);
}

/// <summary>
/// Result of <see cref="IKnowledgeEdgeWriter.PublishApprovedEdgeAsync"/>.
/// Exactly one of <see cref="Published"/>, <see cref="Duplicate"/>, <see cref="Cycle"/>,
/// <see cref="CrossLanguage"/>, or <see cref="NodeMissing"/> will be true; the others false.
/// </summary>
/// <param name="Published">true when a new edge was inserted successfully.</param>
/// <param name="Duplicate">true when the triple already exists (hand-authored or prior approval). Treat as 200 no-op.</param>
/// <param name="Cycle">true when the acyclic guard rejected the edge. No commit. Return 422.</param>
/// <param name="CrossLanguage">true when the cross-language guard rejected the edge. No commit. Return 422.</param>
/// <param name="NodeMissing">true when one or both node IDs could not be resolved in the learning module.
/// Distinct from <see cref="CrossLanguage"/> — the node simply does not exist. Return 422.</param>
/// <param name="EdgeId">The new KnowledgeEdge.Id when Published=true, null otherwise.</param>
public sealed record EdgePublishResult(
    bool Published,
    bool Duplicate,
    bool Cycle,
    bool CrossLanguage,
    bool NodeMissing,
    int? EdgeId);
