using Learnexia.Shared.Contracts.Ai;

namespace Modules.Ai.UnitTests.Fakes;

/// <summary>
/// Deterministic fake <see cref="IAiBatchGateway"/> for unit tests.
///
/// <para>Mirrors the shape of <c>DeterministicFakeAiGateway</c> in <c>Ai.EvalTests</c>.
/// Exercises the submit → poll → result-mapping flow with <b>no live keys</b> so CI
/// can cover the batch gateway contract without any external API call.</para>
///
/// <para>Behaviour:</para>
/// <list type="bullet">
///   <item><see cref="SubmitBatchAsync"/>: when <see cref="SimulateSubmitFailure"/> is
///     false (default) returns a canned <c>BatchJobId = "fake-batch-0001"</c>; when
///     true, returns a typed <see cref="AiErrorKind.Unavailable"/> failure.</item>
///   <item><see cref="PollBatchAsync"/>: first <see cref="ProcessingPollCount"/> calls
///     return <see cref="BatchJobStatus.Processing"/> (simulating an in-progress batch);
///     subsequent calls return <see cref="BatchJobStatus.Ended"/> with one
///     <see cref="BatchItemResult"/> per item in the last submitted request — each item
///     carries a deterministic canned response (<c>"fake-response-{i}"</c>) so tests
///     can assert per-item content without knowing the model output.</item>
///   <item>When <see cref="SimulatePollFailure"/> is true, every poll returns a typed
///     failure regardless of poll count (exercises the fail-closed path).</item>
/// </list>
///
/// <para><strong>Not thread-safe</strong> — designed for single-threaded unit tests only.</para>
/// </summary>
public sealed class DeterministicFakeBatchGateway : IAiBatchGateway
{
    public const string FakeBatchJobId  = "fake-batch-0001";
    public const string FakeContentPrefix = "fake-response-";

    /// <summary>
    /// When true, <see cref="SubmitBatchAsync"/> returns a failure result.
    /// Default: false.
    /// </summary>
    public bool SimulateSubmitFailure { get; set; }

    /// <summary>
    /// When true, <see cref="PollBatchAsync"/> returns a failure result.
    /// Default: false.
    /// </summary>
    public bool SimulatePollFailure { get; set; }

    /// <summary>
    /// Number of poll calls that return <see cref="BatchJobStatus.Processing"/> before
    /// the next call returns <see cref="BatchJobStatus.Ended"/>.
    /// Default: 1 (one "still processing" poll before the results arrive).
    /// </summary>
    public int ProcessingPollCount { get; set; } = 1;

    // ── Observability for tests ────────────────────────────────────────────
    public int SubmitCallCount { get; private set; }
    public int PollCallCount   { get; private set; }

    // Number of items in the last submitted batch (used to build canned results).
    private int _lastBatchSize;

    // ── IAiBatchGateway ────────────────────────────────────────────────────

    /// <inheritdoc />
    public Task<BatchSubmissionResult> SubmitBatchAsync(
        IReadOnlyList<AiRequest> requests,
        CancellationToken cancellationToken = default)
    {
        SubmitCallCount++;
        _lastBatchSize = requests.Count;

        if (SimulateSubmitFailure)
        {
            return Task.FromResult(BatchSubmissionResult.Fail(
                new AiError(AiErrorKind.Unavailable,
                    "DeterministicFakeBatchGateway: SimulateSubmitFailure=true")));
        }

        return Task.FromResult(BatchSubmissionResult.Ok(FakeBatchJobId));
    }

    /// <inheritdoc />
    public Task<BatchPollResult> PollBatchAsync(
        string batchJobId,
        CancellationToken cancellationToken = default)
    {
        PollCallCount++;

        if (SimulatePollFailure)
        {
            return Task.FromResult(BatchPollResult.Fail(
                new AiError(AiErrorKind.Unavailable,
                    "DeterministicFakeBatchGateway: SimulatePollFailure=true")));
        }

        // Return "still processing" for the first ProcessingPollCount calls.
        if (PollCallCount <= ProcessingPollCount)
            return Task.FromResult(BatchPollResult.Processing());

        // Return deterministic canned results.
        var results = new List<BatchItemResult>(_lastBatchSize);
        for (var i = 0; i < _lastBatchSize; i++)
        {
            var usage = new AiUsage
            {
                Provider         = "FakeBatch",
                ModelId          = "fake-batch-model",
                PromptTokens     = 10,
                CompletionTokens = 20,
                LatencyMs        = 0,
                EstimatedCostUsd = 0m,
            };
            results.Add(BatchItemResult.Ok(i, $"{FakeContentPrefix}{i}", usage));
        }

        return Task.FromResult(BatchPollResult.Ended(results));
    }
}
