namespace Learnexia.Host.SystemConfiguration;

/// <summary>
/// OpenTelemetry configuration options (P6-05 Observability).
/// Bound from the <c>OpenTelemetry</c> section in appsettings / env.
///
/// <para>The OTLP exporter is registered ONLY when <see cref="OtlpEndpoint"/> is non-empty;
/// with it absent the app runs with zero behaviour change (no collector required locally or in CI).</para>
///
/// <para>Env vars (double-underscore form for ASP.NET Core config binding):</para>
/// <list type="bullet">
/// <item><c>OpenTelemetry__Otlp__Endpoint</c> — OTLP gRPC/HTTP endpoint (e.g. <c>http://otel-collector:4317</c>). Leave empty = no export.</item>
/// <item><c>OpenTelemetry__ServiceName</c> — service name reported in traces/metrics (default <c>Learnexia</c>).</item>
/// <item><c>OpenTelemetry__SamplingRatio</c> — trace sampling ratio 0.0–1.0 (default <c>1.0</c> = 100%; devops tunes down in prod).</item>
/// </list>
/// </summary>
public sealed class OpenTelemetrySettings
{
    /// <summary>Config section name.</summary>
    public const string SectionName = "OpenTelemetry";

    /// <summary>
    /// OTLP collector endpoint. When empty (the default) the OTLP exporter is NOT registered
    /// and the app runs with no collector (local dev / integration tests).
    /// </summary>
    public OtlpSection Otlp { get; set; } = new();

    /// <summary>
    /// Service name reported in OTel resource attributes.
    /// Default: <c>"Learnexia"</c>.
    /// </summary>
    public string ServiceName { get; set; } = "Learnexia";

    /// <summary>
    /// Trace sampling ratio [0.0, 1.0]. Default: <c>1.0</c> (sample every trace).
    /// Devops should lower this in production to reduce collector load
    /// (e.g. 0.1 = 10% of requests sampled).
    /// </summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>OTLP sub-section.</summary>
    public sealed class OtlpSection
    {
        /// <summary>
        /// OTLP gRPC or HTTP/protobuf endpoint (e.g. <c>http://otel-collector:4317</c>).
        /// Empty = no-op (no exporter registered).
        /// </summary>
        public string Endpoint { get; set; } = string.Empty;
    }
}
