using Learnexia.Shared.Kernel.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Learnexia.Host.HealthChecks;

/// <summary>
/// Readiness health check for MinIO object storage (P6-05 Observability).
/// Performs a lightweight TCP connectivity check against the configured MinIO endpoint.
/// Degraded-tolerant: a reachability failure never causes a hard 503.
///
/// <para>When <see cref="MinIOConfiguration.Enabled"/> is <c>false</c> the check is skipped
/// and reports Healthy (storage intentionally disabled, not a dependency failure).</para>
///
/// <para><strong>Security:</strong> no credentials are emitted in the health-check body —
/// only the endpoint host:port (not the access/secret keys).</para>
/// </summary>
public sealed class MinioHealthCheck : IHealthCheck
{
    private readonly IOptions<MinIOConfiguration> _options;

    public MinioHealthCheck(IOptions<MinIOConfiguration> options)
    {
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var config = _options.Value;

        if (!config.Enabled)
        {
            return HealthCheckResult.Healthy("MinIO is disabled (MinIOConfiguration:Enabled = false).");
        }

        if (string.IsNullOrWhiteSpace(config.Endpoint))
        {
            return HealthCheckResult.Degraded("MinIO endpoint is not configured (MinIOConfiguration:Endpoint is empty).");
        }

        // Lightweight TCP probe: attempt to open a TCP connection to host:port.
        // This does NOT perform an authenticated bucket check (no key access), just confirms
        // the MinIO server is reachable. Degraded on failure so the platform stays up.
        try
        {
            var parts = config.Endpoint.Split(':', 2);
            var host = parts[0];
            var port = parts.Length > 1 && int.TryParse(parts[1], out var p) ? p : (config.UseSSL ? 443 : 9000);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            using var tcpClient = new System.Net.Sockets.TcpClient();
            await tcpClient.ConnectAsync(host, port, cts.Token);

            return HealthCheckResult.Healthy($"MinIO reachable at {host}:{port}.");
        }
        catch (OperationCanceledException)
        {
            return HealthCheckResult.Degraded($"MinIO TCP probe timed out for endpoint '{config.Endpoint}'.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded(
                $"MinIO unreachable at '{config.Endpoint}': {ex.GetType().Name}.");
        }
    }
}
