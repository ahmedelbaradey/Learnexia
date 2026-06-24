using Learnexia.Modules.Curriculum.Application.Abstractions;
using Learnexia.Shared.Contracts.Learning;

namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// Mock-only (no-op) implementation of <see cref="IGraphInferenceClient"/> (BL-03-BE-2).
///
/// <para>There is NO live .NET→Python HTTP call on the infer-edges path. The Python worker
/// polls <c>curriculum.PipelineJobs WHERE JobType='infer_edges'</c> — no HTTP endpoint is
/// exposed by .NET for triggering inference. This implementation exists so that the
/// api-tester / unit tests can inject a deterministic result without a running Python process,
/// exactly mirroring <see cref="NoOpIngestionServiceClient"/> on the ingest path.</para>
///
/// <para>All methods return an empty <see cref="GraphInferenceResult"/> by default. The
/// api-tester or tests replace this with a mock that returns a seeded result.</para>
/// </summary>
public sealed class NoOpGraphInferenceClient : IGraphInferenceClient
{
    /// <inheritdoc />
    /// <remarks>
    /// No-op: always returns an empty result. In production, the Python worker writes
    /// <c>PipelineJob.ResultJson</c> for the <c>'infer_edges'</c> job lane, and
    /// <see cref="Learnexia.Modules.Curriculum.Infrastructure.Jobs.EdgeInferenceAdvanceService"/>
    /// claims + processes it asynchronously — this method is never called in that path.
    /// </remarks>
    public Task<GraphInferenceResult> InferEdgesAsync(
        IReadOnlyList<KnowledgeNodeSummaryDto> nodes,
        CancellationToken ct = default)
        => Task.FromResult(new GraphInferenceResult(
            InferenceModel: "mock",
            InferredEdges: Array.Empty<InferredEdgeDto>()));
}
