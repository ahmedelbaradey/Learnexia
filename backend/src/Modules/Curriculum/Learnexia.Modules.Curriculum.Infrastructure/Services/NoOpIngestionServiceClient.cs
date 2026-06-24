using Learnexia.Modules.Curriculum.Application.Abstractions;

namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// Mock-only (no-op) implementation of <see cref="IIngestionServiceClient"/> (BL-05-BE-3).
///
/// <para>There is NO live .NET→Python HTTP call on the ingest path. The Python ingest worker
/// is self-driven by polling <c>curriculum.PipelineJobs</c> — no HTTP endpoint is exposed
/// by .NET for triggering ingestion. This implementation exists so that the api-tester can
/// inject a deterministic result (simulating a Python worker completing an ingest job)
/// without a running Python process — exactly mirroring <see cref="NoOpParsingServiceClient"/>
/// on the parse path (BL-02 Q10).</para>
///
/// <para>All methods return <c>null</c> by default (no-op). The api-tester replaces this with
/// a mock that returns a seeded <see cref="IngestionJobResult"/> directly.</para>
/// </summary>
public sealed class NoOpIngestionServiceClient : IIngestionServiceClient
{
    /// <inheritdoc />
    /// <remarks>
    /// No-op: always returns <c>null</c>. In production the .NET side reads
    /// <c>PipelineJob.ResultJson</c> directly from the database — it never calls this.
    /// This method exists only as the test-seam entry-point for api-tester mocks.
    /// </remarks>
    public Task<IngestionJobResult?> GetIngestionResultAsync(int jobId, CancellationToken ct = default)
        => Task.FromResult<IngestionJobResult?>(null);
}
