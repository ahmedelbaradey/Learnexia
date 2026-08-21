using FluentAssertions;
using Learnexia.Shared.Contracts.Ai;
using Modules.Ai.UnitTests.Fakes;
using Xunit;

namespace Modules.Ai.UnitTests;

/// <summary>
/// Unit tests for <see cref="DeterministicFakeBatchGateway"/>.
///
/// Validates that the submit → poll → result-mapping flow is exercisable with no live keys
/// (CI-safe) and that the fail-closed paths work as expected.
/// </summary>
public sealed class DeterministicFakeBatchGatewayTests
{
    private static IReadOnlyList<AiRequest> MakeRequests(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new AiRequest { Prompt = $"prompt-{i}", Task = AiTaskKind.Explain })
            .ToList();

    // ── Submit ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task SubmitBatchAsync_ReturnsSuccessWithFakeBatchJobId()
    {
        var gateway = new DeterministicFakeBatchGateway();
        var result  = await gateway.SubmitBatchAsync(MakeRequests(3));

        result.Successed.Should().BeTrue();
        result.BatchJobId.Should().Be(DeterministicFakeBatchGateway.FakeBatchJobId);
        result.Error.Should().BeNull();
    }

    [Fact]
    public async Task SubmitBatchAsync_WhenSimulateSubmitFailure_ReturnsFail()
    {
        var gateway = new DeterministicFakeBatchGateway { SimulateSubmitFailure = true };
        var result  = await gateway.SubmitBatchAsync(MakeRequests(2));

        result.Successed.Should().BeFalse();
        result.BatchJobId.Should().BeNull();
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(AiErrorKind.Unavailable);
    }

    [Fact]
    public async Task SubmitBatchAsync_IncrementsSubmitCallCount()
    {
        var gateway = new DeterministicFakeBatchGateway();
        await gateway.SubmitBatchAsync(MakeRequests(1));
        await gateway.SubmitBatchAsync(MakeRequests(1));

        gateway.SubmitCallCount.Should().Be(2);
    }

    // ── Poll: processing ──────────────────────────────────────────────────

    [Fact]
    public async Task PollBatchAsync_FirstCallReturnsProcessingStatus()
    {
        var gateway = new DeterministicFakeBatchGateway { ProcessingPollCount = 1 };
        await gateway.SubmitBatchAsync(MakeRequests(2));

        var result = await gateway.PollBatchAsync(DeterministicFakeBatchGateway.FakeBatchJobId);

        result.Successed.Should().BeTrue();
        result.Status.Should().Be(BatchJobStatus.Processing);
        result.Results.Should().BeEmpty();
    }

    // ── Poll: ended ───────────────────────────────────────────────────────

    [Fact]
    public async Task PollBatchAsync_AfterProcessingPollCount_ReturnsEndedWithResults()
    {
        const int itemCount = 3;
        var gateway = new DeterministicFakeBatchGateway { ProcessingPollCount = 2 };
        await gateway.SubmitBatchAsync(MakeRequests(itemCount));

        // First two polls: processing.
        await gateway.PollBatchAsync(DeterministicFakeBatchGateway.FakeBatchJobId);
        await gateway.PollBatchAsync(DeterministicFakeBatchGateway.FakeBatchJobId);

        // Third poll: ended.
        var result = await gateway.PollBatchAsync(DeterministicFakeBatchGateway.FakeBatchJobId);

        result.Successed.Should().BeTrue();
        result.Status.Should().Be(BatchJobStatus.Ended);
        result.Results.Should().HaveCount(itemCount);
    }

    [Fact]
    public async Task PollBatchAsync_EndedResults_AreOrderedByIndexAndHaveCannedContent()
    {
        const int itemCount = 4;
        var gateway = new DeterministicFakeBatchGateway { ProcessingPollCount = 0 };
        await gateway.SubmitBatchAsync(MakeRequests(itemCount));

        var result = await gateway.PollBatchAsync(DeterministicFakeBatchGateway.FakeBatchJobId);

        result.Results.Should().HaveCount(itemCount);
        for (var i = 0; i < itemCount; i++)
        {
            var item = result.Results[i];
            item.Index.Should().Be(i);
            item.Successed.Should().BeTrue();
            item.Content.Should().Be($"{DeterministicFakeBatchGateway.FakeContentPrefix}{i}");
            item.Error.Should().BeNull();
            item.Usage.Should().NotBeNull();
            item.Usage!.Provider.Should().Be("FakeBatch");
        }
    }

    // ── Poll: failure ─────────────────────────────────────────────────────

    [Fact]
    public async Task PollBatchAsync_WhenSimulatePollFailure_ReturnsFail()
    {
        var gateway = new DeterministicFakeBatchGateway { SimulatePollFailure = true };
        await gateway.SubmitBatchAsync(MakeRequests(2));

        var result = await gateway.PollBatchAsync(DeterministicFakeBatchGateway.FakeBatchJobId);

        result.Successed.Should().BeFalse();
        result.Status.Should().Be(BatchJobStatus.Unknown);
        result.Error.Should().NotBeNull();
        result.Error!.Kind.Should().Be(AiErrorKind.Unavailable);
    }

    // ── Full round-trip ───────────────────────────────────────────────────

    [Fact]
    public async Task FullRoundTrip_SubmitThenPollUntilEnded_ReturnsAllResults()
    {
        const int itemCount = 5;
        var gateway = new DeterministicFakeBatchGateway { ProcessingPollCount = 1 };

        // Submit.
        var submitResult = await gateway.SubmitBatchAsync(MakeRequests(itemCount));
        submitResult.Successed.Should().BeTrue();
        var jobId = submitResult.BatchJobId!;

        // Poll loop (mirrors what the real job would do).
        BatchPollResult pollResult;
        do
        {
            pollResult = await gateway.PollBatchAsync(jobId);
            pollResult.Successed.Should().BeTrue();
        }
        while (pollResult.Status == BatchJobStatus.Processing);

        // Assert results.
        pollResult.Status.Should().Be(BatchJobStatus.Ended);
        pollResult.Results.Should().HaveCount(itemCount);
        pollResult.Results.Select(r => r.Index).Should().BeInAscendingOrder();
        pollResult.Results.All(r => r.Successed).Should().BeTrue();
    }
}
