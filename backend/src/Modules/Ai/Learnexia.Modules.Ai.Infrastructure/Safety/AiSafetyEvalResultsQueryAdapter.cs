using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Ai.Infrastructure.Safety;

/// <summary>
/// Implements <see cref="IAiSafetyEvalResultsQuery"/> by reading the committed
/// <c>safety-eval-results.json</c> artifact embedded in this assembly.
///
/// <para><strong>Mechanism (Option 1 from the brief §C decision):</strong>
/// The <c>Ai.EvalTests</c> harness (P6-02) writes the artifact to
/// <c>backend/tests/Ai.EvalTests/Data/safety-eval-results.json</c> and also updates
/// <c>backend/src/Modules/Ai/Learnexia.Modules.Ai.Infrastructure/EvalResults/safety-eval-results.json</c>
/// (the source copy). The infrastructure .csproj embeds that source copy as a resource
/// (logical name <c>safety-eval-results.json</c>) so the runtime can read it without any
/// DB table or migration — the results travel with the binary.</para>
///
/// <para><strong>No DB, no migration.</strong> Trend-across-runs is a documented follow-up
/// (Option 2 from the brief §C, if the lead requests it later).</para>
///
/// <para><strong>Sentinel-safe:</strong> if the embedded resource is absent or malformed
/// (e.g. bootstrap/placeholder state), returns <see cref="SafetyEvalRunResult.Bootstrap()"/> —
/// never null, never throws. The admin endpoint returns HTTP 200 with the bootstrap sentinel,
/// not a 404 or 500.</para>
///
/// <para><strong>Cross-module isolation:</strong> all consumers inject <see cref="IAiSafetyEvalResultsQuery"/>
/// from <c>Shared.Contracts</c> — they never reference this adapter or any Ai project directly.</para>
///
/// <para>Registered as Scoped in <c>AddAiInfrastructure</c> (mirrors <c>IPlatformAiSafetyStatsQuery</c>).</para>
/// </summary>
public sealed class AiSafetyEvalResultsQueryAdapter : IAiSafetyEvalResultsQuery
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Logical name set in the .csproj <EmbeddedResource LogicalName="safety-eval-results.json">.
    private const string ResourceLogicalName = "safety-eval-results.json";

    private readonly ILoggerManager _logger;

    public AiSafetyEvalResultsQueryAdapter(ILoggerManager logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<SafetyEvalRunResult> GetLatestAsync(CancellationToken ct = default)
    {
        try
        {
            var assembly = typeof(AiSafetyEvalResultsQueryAdapter).Assembly;

            // The logical name is set in the .csproj; fall back to scanning by suffix.
            var resourceName = FindResourceName(assembly);

            if (resourceName is null)
            {
                _logger.LogWarn(
                    "AiSafetyEvalResultsQueryAdapter: embedded resource 'safety-eval-results.json' not found. " +
                    "Returning bootstrap sentinel. Run: dotnet test --filter Category=EvalOffline to populate it.");
                return Task.FromResult(SafetyEvalRunResult.Bootstrap());
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                _logger.LogWarn("AiSafetyEvalResultsQueryAdapter: resource stream is null. Returning bootstrap sentinel.");
                return Task.FromResult(SafetyEvalRunResult.Bootstrap());
            }

            var dto = JsonSerializer.Deserialize<EvalResultsJsonDto>(stream, JsonOpts);
            if (dto is null)
            {
                _logger.LogWarn("AiSafetyEvalResultsQueryAdapter: deserialization returned null. Returning bootstrap sentinel.");
                return Task.FromResult(SafetyEvalRunResult.Bootstrap());
            }

            // Bootstrap sentinel check: if no real run has been recorded yet (RunId == Guid.Empty),
            // return the Bootstrap value so consumers get a clean "not yet run" signal.
            if (dto.RunId == Guid.Empty)
            {
                _logger.LogInfo(
                    "AiSafetyEvalResultsQueryAdapter: embedded artifact is the bootstrap placeholder. " +
                    "Run: dotnet test --filter Category=EvalOffline to populate it.");
                return Task.FromResult(SafetyEvalRunResult.Bootstrap());
            }

            return Task.FromResult(MapToResult(dto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AiSafetyEvalResultsQueryAdapter: unexpected error reading embedded artifact. Returning bootstrap sentinel.");
            return Task.FromResult(SafetyEvalRunResult.Bootstrap());
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static string? FindResourceName(Assembly assembly)
    {
        // First try the exact logical name (set in .csproj).
        var names = assembly.GetManifestResourceNames();
        return Array.Find(names, n => n.Equals(ResourceLogicalName, StringComparison.OrdinalIgnoreCase))
            ?? Array.Find(names, n => n.EndsWith(ResourceLogicalName, StringComparison.OrdinalIgnoreCase));
    }

    private static SafetyEvalRunResult MapToResult(EvalResultsJsonDto dto)
    {
        return new SafetyEvalRunResult(
            RunId:            dto.RunId,
            RanAtUtc:         dto.RanAt,
            TotalCases:       dto.TotalCases,
            PassedCases:      dto.PassedCases,
            FailedCases:      dto.FailedCases,
            PassRate:         dto.PassRate,
            FailRate:         dto.FailRate,
            ThresholdPercent: dto.ThresholdPercent,
            Breached:         dto.Breached,
            Tier:             dto.Tier ?? "EvalOffline",
            Note:             dto.Note ?? string.Empty,
            ByCheck:    MapBreakdown(dto.ByCheck),
            BySubject:  MapBreakdown(dto.BySubject),
            ByLanguage: MapBreakdown(dto.ByLanguage));
    }

    private static IReadOnlyDictionary<string, EvalCheckBreakdown> MapBreakdown(
        Dictionary<string, EvalBreakdownJsonEntry>? raw)
    {
        if (raw is null || raw.Count == 0)
            return new Dictionary<string, EvalCheckBreakdown>();

        return raw.ToDictionary(
            kv => kv.Key,
            kv => new EvalCheckBreakdown(
                kv.Value.Passed,
                kv.Value.Total,
                kv.Value.Total == 0 ? 0.0 : (double)kv.Value.Passed / kv.Value.Total * 100.0));
    }

    // ── JSON deserialization models ────────────────────────────────────────────

    private sealed class EvalResultsJsonDto
    {
        [JsonPropertyName("runId")]
        public Guid RunId { get; init; }

        [JsonPropertyName("ranAt")]
        public DateTime RanAt { get; init; }

        [JsonPropertyName("totalCases")]
        public int TotalCases { get; init; }

        [JsonPropertyName("passedCases")]
        public int PassedCases { get; init; }

        [JsonPropertyName("failedCases")]
        public int FailedCases { get; init; }

        [JsonPropertyName("passRate")]
        public double PassRate { get; init; }

        [JsonPropertyName("failRate")]
        public double FailRate { get; init; }

        [JsonPropertyName("thresholdPercent")]
        public double ThresholdPercent { get; init; }

        [JsonPropertyName("breached")]
        public bool Breached { get; init; }

        [JsonPropertyName("tier")]
        public string? Tier { get; init; }

        [JsonPropertyName("note")]
        public string? Note { get; init; }

        [JsonPropertyName("byCheck")]
        public Dictionary<string, EvalBreakdownJsonEntry>? ByCheck { get; init; }

        [JsonPropertyName("bySubject")]
        public Dictionary<string, EvalBreakdownJsonEntry>? BySubject { get; init; }

        [JsonPropertyName("byLanguage")]
        public Dictionary<string, EvalBreakdownJsonEntry>? ByLanguage { get; init; }
    }

    private sealed class EvalBreakdownJsonEntry
    {
        [JsonPropertyName("passed")]
        public int Passed { get; init; }

        [JsonPropertyName("total")]
        public int Total { get; init; }
    }
}
