using Learnexia.Modules.Curriculum.Application.Abstractions;

namespace Learnexia.Modules.Curriculum.Infrastructure.Services;

/// <summary>
/// Mock-only (no-op) implementation of <see cref="IParsingServiceClient"/> (BL-02-BE-2, Q10).
///
/// <para>There is NO live .NET→Python HTTP call on the parse path. The Python worker
/// is self-driven by polling <c>curriculum.PipelineJobs</c> — no HTTP endpoint is exposed.
/// This implementation exists so that the api-tester can inject a deterministic result
/// (simulating a Python worker completing a job) without a running Python process.</para>
///
/// <para>All methods return <c>null</c> by default (no-op). The api-tester replaces this
/// with a mock that returns a seeded <see cref="ParseArtifactResult"/> directly.</para>
/// </summary>
public sealed class NoOpParsingServiceClient : IParsingServiceClient
{
    /// <inheritdoc />
    /// <remarks>
    /// No-op: always returns <c>null</c>. In production the .NET side reads
    /// <c>PipelineJob.ResultJson</c> directly from the database — it never calls this.
    /// This method exists only as the test-seam entrypoint for api-tester mocks.
    /// </remarks>
    public Task<ParseArtifactResult?> GetParseResultAsync(int jobId, CancellationToken ct = default)
        => Task.FromResult<ParseArtifactResult?>(null);
}
