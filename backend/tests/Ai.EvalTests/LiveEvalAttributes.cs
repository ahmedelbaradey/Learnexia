using Xunit;

namespace Ai.EvalTests;

/// <summary>
/// Custom xUnit <see cref="TheoryAttribute"/> that automatically skips live-eval tests
/// when no real AI provider key is present in the environment.
///
/// <para><strong>Skip condition:</strong> both <c>Ai__Providers__Claude__ApiKey</c> and
/// <c>Ai__Providers__OpenAi__ApiKey</c> environment variables are absent or empty.
/// When either key is present the test runs normally; when neither is set xUnit reports
/// the test as SKIPPED (not FAILED), so CI never sees a red build from missing keys.</para>
///
/// <para><strong>Usage:</strong> annotate live-tier Theory tests with
/// <c>[LiveEvalTheory]</c> instead of <c>[Theory]</c>. For single-case assertions
/// use <c>[LiveEvalFact]</c> instead of <c>[Fact]</c>.</para>
///
/// <para><strong>How to run:</strong>
/// <code>
/// Ai__Providers__Claude__ApiKey=sk-ant-... dotnet test backend/tests/Ai.EvalTests --filter Category=EvalLive
/// </code>
/// Never run with real keys in CI — the live tier is devops/launch Gate B only.</para>
/// </summary>
public sealed class LiveEvalTheoryAttribute : TheoryAttribute
{
    private const string ClaudeKeyEnvVar = "Ai__Providers__Claude__ApiKey";
    private const string OpenAiKeyEnvVar = "Ai__Providers__OpenAi__ApiKey";

    public LiveEvalTheoryAttribute()
    {
        if (!HasAnyKey())
        {
            Skip =
                "EvalLive tier skipped: no provider key found. " +
                $"Set {ClaudeKeyEnvVar} or {OpenAiKeyEnvVar} to run the live eval tier. " +
                "This is devops/launch Gate B only — never run in CI.";
        }
    }

    private static bool HasAnyKey()
    {
        var claudeKey = Environment.GetEnvironmentVariable(ClaudeKeyEnvVar);
        var openAiKey = Environment.GetEnvironmentVariable(OpenAiKeyEnvVar);

        return !string.IsNullOrWhiteSpace(claudeKey) || !string.IsNullOrWhiteSpace(openAiKey);
    }
}

/// <summary>
/// Custom xUnit <see cref="FactAttribute"/> that automatically skips live-eval tests
/// when no real AI provider key is present in the environment.
///
/// <para>Mirrors the skip-gate logic of <see cref="LiveEvalTheoryAttribute"/>.</para>
/// </summary>
public sealed class LiveEvalFactAttribute : FactAttribute
{
    private const string ClaudeKeyEnvVar = "Ai__Providers__Claude__ApiKey";
    private const string OpenAiKeyEnvVar = "Ai__Providers__OpenAi__ApiKey";

    public LiveEvalFactAttribute()
    {
        if (!HasAnyKey())
        {
            Skip =
                "EvalLive tier skipped: no provider key found. " +
                $"Set {ClaudeKeyEnvVar} or {OpenAiKeyEnvVar} to run the live eval tier. " +
                "This is devops/launch Gate B only — never run in CI.";
        }
    }

    private static bool HasAnyKey()
    {
        var claudeKey = Environment.GetEnvironmentVariable(ClaudeKeyEnvVar);
        var openAiKey = Environment.GetEnvironmentVariable(OpenAiKeyEnvVar);

        return !string.IsNullOrWhiteSpace(claudeKey) || !string.IsNullOrWhiteSpace(openAiKey);
    }
}
