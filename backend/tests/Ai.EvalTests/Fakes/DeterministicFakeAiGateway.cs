using Learnexia.Shared.Contracts.Ai;

namespace Ai.EvalTests.Fakes;

/// <summary>
/// Deterministic fake <see cref="IAiGateway"/> for the P6-02 offline eval harness.
///
/// <para>Instead of calling a real provider or always failing (the v0 no-key approach),
/// this fake returns a per-case canned judge JSON string stored as <c>expectedJudgeVerdict</c>
/// in the eval set. The real check parse/map/fail-closed logic is exercised against
/// this canned response — which is exactly what we own and can regress on.</para>
///
/// <para>What this validates:</para>
/// <list type="bullet">
/// <item>The check correctly parses the judge JSON (toxic/inappropriate boolean, severity).</item>
/// <item>The check maps the verdict to the right <see cref="CheckOutcome"/>
///   (Pass / Block / NeedsRegeneration).</item>
/// <item>The fail-closed path is exercised when <c>expectedJudgeVerdict</c> is null
///   (simulates a gateway failure — the check must Block, never Pass).</item>
/// </list>
///
/// <para>What this does NOT validate: the real model's judgment on the content.
/// That is the devops-gated live tier (Gate B). CI green != AI safety proven.</para>
/// </summary>
public sealed class DeterministicFakeAiGateway : IAiGateway
{
    private readonly string? _cannedResponse;

    /// <summary>
    /// Creates a fake that returns <paramref name="cannedJudgeVerdict"/> as the completion.
    /// Pass <see langword="null"/> to simulate a gateway failure (exercises the fail-closed path).
    /// </summary>
    public DeterministicFakeAiGateway(string? cannedJudgeVerdict)
    {
        _cannedResponse = cannedJudgeVerdict;
    }

    /// <inheritdoc />
    public Task<AiResult> CompleteAsync(AiRequest request, CancellationToken cancellationToken = default)
    {
        if (_cannedResponse is null)
        {
            // Simulate gateway failure — the check must fail-closed (Block), never Pass.
            return Task.FromResult(AiResult.Fail(
                new AiError(AiErrorKind.Unavailable, "DeterministicFakeAiGateway: no canned response (fail-closed path)")));
        }

        return Task.FromResult(AiResult.Ok(
            _cannedResponse,
            new AiUsage { Provider = "Fake", ModelId = "fake-eval", PromptTokens = 0, CompletionTokens = 0 }));
    }

    /// <inheritdoc />
    /// <remarks>Not used by the safety checks — throws to detect accidental usage.</remarks>
    public IAsyncEnumerable<AiChunk> StreamAsync(AiRequest request, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("DeterministicFakeAiGateway does not support streaming.");
}
