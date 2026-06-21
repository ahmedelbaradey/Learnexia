using Learnexia.Shared.Contracts.Ai;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Learnexia.Host.HealthChecks;

/// <summary>
/// Readiness health check for the AI Gateway (P6-05 Observability).
/// Reports whether the AI Tutor is configured (provider key present) without making any
/// paid model call — config-inspection only, no real keys needed in CI/dev.
///
/// <para><strong>Result mapping:</strong>
/// <list type="bullet">
/// <item>Provider configured → <see cref="HealthStatus.Healthy"/></item>
/// <item>Provider NOT configured → <see cref="HealthStatus.Degraded"/> (AI Tutor dormant,
///   platform is still up — NOT a hard 503). Matches the Redis Degraded-tolerant pattern
///   at <c>Program.cs</c>.</item>
/// </list>
/// </para>
///
/// <para><strong>Module isolation:</strong> injects <see cref="IAiReadinessProbe"/>
/// from <c>Shared.Contracts</c> only — never an Ai-module internal type.</para>
///
/// <para><strong>Security:</strong> the health-check data includes only booleans; no API key
/// values or secrets are exposed in the response body.</para>
/// </summary>
public sealed class AiGatewayHealthCheck : IHealthCheck
{
    private readonly IAiReadinessProbe _probe;

    public AiGatewayHealthCheck(IAiReadinessProbe probe)
    {
        _probe = probe;
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        AiReadinessStatus status;
        try
        {
            status = _probe.GetStatus();
        }
        catch (Exception ex)
        {
            // Any unexpected error reading config → Degraded (never a hard 503).
            return Task.FromResult(HealthCheckResult.Degraded(
                $"AI Gateway readiness probe threw: {ex.GetType().Name}"));
        }

        if (status.ProviderConfigured)
        {
            var ragNote = status.RagConfigured
                ? "RAG context configured."
                : "RAG context dormant (EmptyLearningContextProvider).";
            return Task.FromResult(HealthCheckResult.Healthy(
                $"AI Gateway configured. Provider: {status.DefaultProvider}. {ragNote}"));
        }

        // No provider key → Degraded (AI Tutor dormant, platform is running fine).
        return Task.FromResult(HealthCheckResult.Degraded(
            $"AI Gateway: no provider API key configured (provider: {status.DefaultProvider}). " +
            "AI Tutor is dormant. Set Ai__Providers__Claude__ApiKey or Ai__Providers__OpenAi__ApiKey."));
    }
}
