namespace Learnexia.Modules.Curriculum.Application.Abstractions;

/// <summary>
/// Mockable test seam for the Python curriculum-intelligence parsing worker (BL-02-BE-2).
///
/// <para><strong>No live HTTP call is made via this interface.</strong>
/// The cross-process transport is the <c>curriculum.PipelineJobs</c> DB-outbox (ADR-0004, §4b).
/// The Python worker polls the outbox directly — there is no .NET→Python HTTP endpoint to call.
/// <see cref="IParsingServiceClient"/> exists ONLY as a test seam so that the api-tester can
/// simulate completed parse jobs without a running Python worker (Q10 resolution).</para>
///
/// <para>The only registered implementation is <c>NoOpParsingServiceClient</c> (mock/no-op),
/// which returns a deterministic result in dev + CI. A live HTTP implementation is NOT
/// registered and NOT planned — the Python side is self-driven by polling.</para>
/// </summary>
public interface IParsingServiceClient
{
    /// <summary>
    /// Simulate/retrieve the parse result for the given job (test seam only).
    /// Returns a deterministic <see cref="ParseArtifactResult"/> in the mock implementation.
    /// </summary>
    /// <param name="jobId">The <c>PipelineJob.Id</c> whose result is being retrieved.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ParseArtifactResult?> GetParseResultAsync(int jobId, CancellationToken ct = default);
}

/// <summary>
/// Represents the parsed artifact result that the Python worker writes into
/// <c>PipelineJob.ResultJson</c> and the .NET advance poller reads (BL-02-BE-2, BE-3).
///
/// <para>Shape mirrors the DB-outbox contract from <c>curriculum-system-of-record.md §4b</c>:
/// <code>
/// {
///   "artifact_key": "...",
///   "chapters": [{ "number": 1, "title": "...", "page_start": 1, "page_end": 10 }],
///   "parse_status": "done",
///   "diagnostics": null
/// }
/// </code>
/// </para>
/// </summary>
/// <param name="ArtifactKey">MinIO object key of the JSON artifact in the <c>curriculum</c> bucket.</param>
/// <param name="Chapters">Chapter summaries extracted from the document.</param>
/// <param name="ParseStatus">Terminal status written by Python: <c>"done"</c> or <c>"failed"</c>.</param>
/// <param name="Diagnostics">Human-readable diagnostics on failure; null on success.</param>
public record ParseArtifactResult(
    string ArtifactKey,
    IReadOnlyList<ParsedChapterItem> Chapters,
    string ParseStatus,
    string? Diagnostics);

/// <summary>
/// One chapter entry in the parse artifact, as returned by the Python worker (BE-3/BE-6).
///
/// <para>Maps directly to the <c>chapters[]</c> array in <c>ResultJson</c>:
/// <c>{ "number": 1, "title": "Fractions", "page_start": 1, "page_end": 20 }</c></para>
/// </summary>
/// <param name="Number">Chapter number (1-based).</param>
/// <param name="Title">Human-readable chapter title.</param>
/// <param name="PageStart">Optional first page number.</param>
/// <param name="PageEnd">Optional last page number.</param>
public record ParsedChapterItem(
    int Number,
    string Title,
    int? PageStart,
    int? PageEnd);
