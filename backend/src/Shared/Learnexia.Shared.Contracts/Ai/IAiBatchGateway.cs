namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Offline / batch AI completion seam. Backed by the Anthropic Batch API (~50% cheaper
/// than synchronous calls) for non-interactive bulk work such as cache pre-warming and
/// bulk question generation.
///
/// <para>Contract is <b>separate from <see cref="IAiGateway"/></b> so the runtime
/// student-facing path and the offline batch path are independently evolvable and
/// independently testable without touching each other.</para>
///
/// <para><b>Never throws</b> — failures are encoded in the result types
/// (<see cref="BatchSubmissionResult.Successed"/> / <see cref="BatchPollResult.Successed"/>).</para>
///
/// <para>Activation is devops-gated: the implementation is dormant when no Batch API key is
/// configured. Dormant state does NOT crash the host (mirrors the runtime
/// <see cref="IAiGateway"/> degrade behavior).</para>
///
/// <para>This interface lives in <c>Shared.Contracts</c> so any module can inject it
/// without referencing <c>Ai.Infrastructure</c> (module isolation rule).</para>
/// </summary>
public interface IAiBatchGateway
{
    /// <summary>
    /// Submits a batch of AI requests to the Anthropic Batch API.
    ///
    /// <para>Returns a <see cref="BatchSubmissionResult"/> containing the provider-assigned
    /// <c>BatchJobId</c> on success, or a typed error on failure.</para>
    ///
    /// <para>Never throws — failures are encoded in
    /// <see cref="BatchSubmissionResult.Successed"/> / <see cref="BatchSubmissionResult.Error"/>.</para>
    /// </summary>
    Task<BatchSubmissionResult> SubmitBatchAsync(
        IReadOnlyList<AiRequest> requests,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Polls the status and (when complete) retrieves the results for a previously
    /// submitted batch job.
    ///
    /// <para>When the batch is still processing,
    /// <see cref="BatchPollResult.Status"/> is <see cref="BatchJobStatus.Processing"/>
    /// and <see cref="BatchPollResult.Results"/> is empty.</para>
    ///
    /// <para>When the batch is complete (succeeded or partially failed),
    /// <see cref="BatchPollResult.Status"/> is <see cref="BatchJobStatus.Ended"/>
    /// and <see cref="BatchPollResult.Results"/> contains one
    /// <see cref="BatchItemResult"/> per original request (in the same order).</para>
    ///
    /// <para>Never throws — failures are encoded in
    /// <see cref="BatchPollResult.Successed"/> / <see cref="BatchPollResult.Error"/>.</para>
    /// </summary>
    Task<BatchPollResult> PollBatchAsync(
        string batchJobId,
        CancellationToken cancellationToken = default);
}

// ---------------------------------------------------------------------------
// Result DTOs (mirror AiResult / AiError shape)
// ---------------------------------------------------------------------------

/// <summary>
/// Result of a <see cref="IAiBatchGateway.SubmitBatchAsync"/> call.
/// Mirrors the <c>Successed</c> flag convention used across <c>BaseResponse&lt;T&gt;</c>.
/// </summary>
public sealed record BatchSubmissionResult
{
    /// <summary>True when the batch was accepted by the provider.</summary>
    public required bool Successed { get; init; }

    /// <summary>
    /// Provider-assigned batch job identifier.
    /// Non-null only when <see cref="Successed"/> is true.
    /// </summary>
    public string? BatchJobId { get; init; }

    /// <summary>Error descriptor. Non-null when <see cref="Successed"/> is false.</summary>
    public AiError? Error { get; init; }

    // --- Factory helpers ---

    public static BatchSubmissionResult Ok(string batchJobId) =>
        new() { Successed = true, BatchJobId = batchJobId };

    public static BatchSubmissionResult Fail(AiError error) =>
        new() { Successed = false, Error = error };
}

/// <summary>
/// Result of a <see cref="IAiBatchGateway.PollBatchAsync"/> call.
/// </summary>
public sealed record BatchPollResult
{
    /// <summary>True when the poll call itself succeeded (even if some items failed).</summary>
    public required bool Successed { get; init; }

    /// <summary>Current status of the batch job.</summary>
    public BatchJobStatus Status { get; init; }

    /// <summary>
    /// Per-request results. Populated (possibly partially) when
    /// <see cref="Status"/> is <see cref="BatchJobStatus.Ended"/>.
    /// Empty when the batch is still processing.
    /// </summary>
    public IReadOnlyList<BatchItemResult> Results { get; init; } = [];

    /// <summary>Error descriptor. Non-null when <see cref="Successed"/> is false.</summary>
    public AiError? Error { get; init; }

    // --- Factory helpers ---

    public static BatchPollResult Processing() =>
        new() { Successed = true, Status = BatchJobStatus.Processing, Results = [] };

    public static BatchPollResult Ended(IReadOnlyList<BatchItemResult> results) =>
        new() { Successed = true, Status = BatchJobStatus.Ended, Results = results };

    public static BatchPollResult Fail(AiError error) =>
        new() { Successed = false, Status = BatchJobStatus.Unknown, Error = error };
}

/// <summary>
/// Result for a single item in a completed batch job.
/// Mirrors <see cref="AiResult"/> for per-item status.
/// </summary>
public sealed record BatchItemResult
{
    /// <summary>
    /// Zero-based index of this result, corresponding to the position of the original
    /// <see cref="AiRequest"/> in the list passed to
    /// <see cref="IAiBatchGateway.SubmitBatchAsync"/>.
    /// </summary>
    public required int Index { get; init; }

    /// <summary>True when this individual item succeeded.</summary>
    public required bool Successed { get; init; }

    /// <summary>
    /// Completion text from the provider for this item.
    /// Null when <see cref="Successed"/> is false.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>Typed error for this item. Non-null when <see cref="Successed"/> is false.</summary>
    public AiError? Error { get; init; }

    /// <summary>Usage telemetry for this item (best-effort; may be null on failure).</summary>
    public AiUsage? Usage { get; init; }

    // --- Factory helpers ---

    public static BatchItemResult Ok(int index, string content, AiUsage? usage = null) =>
        new() { Index = index, Successed = true, Content = content, Usage = usage };

    public static BatchItemResult Fail(int index, AiError error) =>
        new() { Index = index, Successed = false, Error = error };
}

/// <summary>Status of a batch job returned by <see cref="IAiBatchGateway.PollBatchAsync"/>.</summary>
public enum BatchJobStatus
{
    /// <summary>Status could not be determined (poll call failed).</summary>
    Unknown = 0,

    /// <summary>The batch is still being processed by the provider.</summary>
    Processing = 1,

    /// <summary>The batch has ended (all items succeeded or failed).</summary>
    Ended = 2,
}
